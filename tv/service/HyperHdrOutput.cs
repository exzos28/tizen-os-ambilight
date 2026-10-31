using System;
using System.Net.Sockets;
using System.Threading;

namespace Service
{
    // Streams frames to a HyperHDR server as images over its FlatBuffers TCP input.
    //
    // Threading: Offer() only swaps the newest frame into a one-slot mailbox and never
    // blocks, so a slow or dead server cannot stall capture. A sender thread connects,
    // registers, renders and sends; a reader thread per connection consumes HyperHDR's
    // replies (it answers every message, and unread replies would pile up on the server).
    //
    // Liveness: a message that gets no reply within LivenessMs means the connection is dead
    // (e.g. the HyperHDR host lost power and TCP would keep "sending" into the kernel buffer
    // for minutes). Reconnects back off 1 s → 2 s → 4 s → 5 s (cap) and reset once a reply
    // arrives. Closing the socket is enough to release our priority in HyperHDR.
    sealed class HyperHdrOutput : IDisposable
    {
        private const int ConnectTimeoutMs = 3000;
        private const int SendTimeoutMs = 2000;
        private const int LivenessMs = 3000;
        private const int MaxBackoffMs = 5000;
        private const int MaxReplyBytes = 64 * 1024;

        private readonly HyperHdrSettings _settings;
        private readonly Action<string, string> _log;
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Thread _sender;

        private EdgeFrame _pending;          // mailbox, swapped atomically
        private Socket _socket;              // owned by the sender thread
        private int _generation;             // bumps on every (re)connect; stale readers ignore themselves
        private long _sent, _replied;        // messages awaiting a reply = _sent - _replied
        private long _waitingSinceTicks;     // when the oldest unanswered message was sent (or last reply)
        private long _connectedAtTicks;
        private long _nextConnectTicks;
        private int _backoffMs = 1000;
        private volatile string _dropReason;  // set by the reader thread
        private long _replyErrors;
        private int _consecutiveFailures;     // reset by the first reply of a connection

        // Guards the per-connection state (_generation, counters, _dropReason) so a reader
        // of an old connection can't update it after the sender has moved to a new one.
        private readonly object _stateLock = new object();

        public HyperHdrOutput(HyperHdrSettings settings, Action<string, string> log)
        {
            _settings = settings;
            _log = log;
        }

        public string Endpoint => $"{_settings.Host}:{_settings.Port}";

        // Number of replies received so far (for tests and diagnostics).
        public long RepliesReceived { get { lock (_stateLock) return _replied; } }
        public bool IsConnected => Volatile.Read(ref _socket) != null;

        public void Start()
        {
            _sender = new Thread(SenderLoop) { IsBackground = true, Name = "HyperHDR sender" };
            _sender.Start();
        }

        public void Offer(EdgeFrame frame)
        {
            Interlocked.Exchange(ref _pending, frame);
            _wake.Set();
        }

        // Skip the current backoff and try to connect right away (e.g. user pressed Start).
        public void ReconnectNow()
        {
            Interlocked.Exchange(ref _nextConnectTicks, 0);
            _wake.Set();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _wake.Set();
            _sender?.Join(ConnectTimeoutMs + 1000);
            CloseSocket();
        }

        private void SenderLoop()
        {
            var token = _cts.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_socket == null)
                    {
                        long wait = Interlocked.Read(ref _nextConnectTicks) - Environment.TickCount64;
                        if (wait > 0) { WaitHandle.WaitAny(new[] { _wake, token.WaitHandle }, (int)Math.Min(wait, 1000)); continue; }
                        Connect();
                        continue;
                    }

                    WaitHandle.WaitAny(new[] { _wake, token.WaitHandle }, 500);
                    if (token.IsCancellationRequested) break;

                    string reason;
                    bool stale;
                    lock (_stateLock)
                    {
                        reason = _dropReason;
                        stale = _sent > _replied && Environment.TickCount64 - _waitingSinceTicks > LivenessMs;
                    }
                    if (reason != null) { Drop(reason); continue; }
                    if (stale) { Drop($"no reply for {LivenessMs} ms"); continue; }

                    var frame = Interlocked.Exchange(ref _pending, null);
                    if (frame == null) continue;

                    var rgb = EdgeImage.Render(frame, _settings.ImageWidth, _settings.ImageHeight);
                    Send(HyperHdrProtocol.EncodeRawImage(rgb, _settings.ImageWidth, _settings.ImageHeight));
                }
                catch (Exception ex)
                {
                    // After Dispose() the socket is closed under us — just leave.
                    if (token.IsCancellationRequested) break;
                    Drop(ex.Message);
                }
            }
        }

        private void Connect()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,   // one small message per frame — don't let Nagle hold it back
                SendTimeout = SendTimeoutMs,
            };
            try
            {
                var connect = socket.ConnectAsync(_settings.Host, _settings.Port);
                if (!connect.Wait(ConnectTimeoutMs))
                    throw new TimeoutException($"connect timed out after {ConnectTimeoutMs} ms");
            }
            catch (Exception ex)
            {
                socket.Dispose();
                ScheduleReconnect($"connect to {Endpoint} failed: {(ex as AggregateException)?.InnerException?.Message ?? ex.Message}");
                return;
            }

            int generation;
            lock (_stateLock)
            {
                generation = ++_generation;
                _sent = 0;
                _replied = 0;
                _dropReason = null;
                _connectedAtTicks = Environment.TickCount64;
                Volatile.Write(ref _socket, socket);
            }
            new Thread(() => ReaderLoop(socket, generation)) { IsBackground = true, Name = "HyperHDR reader" }.Start();

            Send(HyperHdrProtocol.EncodeRegister(_settings.Origin, _settings.Priority));
            _log("HyperHdrConnected", $"{Endpoint} priority={_settings.Priority} image={_settings.ImageWidth}x{_settings.ImageHeight}");
        }

        private void Send(byte[] message)
        {
            var socket = Volatile.Read(ref _socket) ?? throw new ObjectDisposedException("socket");
            lock (_stateLock)
            {
                if (_sent == _replied) _waitingSinceTicks = Environment.TickCount64;   // first unanswered message starts the clock
                _sent++;
            }
            socket.Send(HyperHdrProtocol.Frame(message));
        }

        private void ReaderLoop(Socket socket, int generation)
        {
            var prefix = new byte[4];
            try
            {
                while (true)
                {
                    ReadExactly(socket, prefix);
                    int length = HyperHdrProtocol.ReadLength(prefix);
                    if (length <= 0 || length > MaxReplyBytes) throw new InvalidOperationException($"bad reply length {length}");
                    var body = new byte[length];
                    ReadExactly(socket, body);

                    var reply = HyperHdrProtocol.DecodeReply(body);
                    lock (_stateLock)
                    {
                        if (generation != _generation) return;
                        _replied++;
                        _waitingSinceTicks = Environment.TickCount64;
                    }
                    // The server is really talking to us: reset backoff and failure count
                    // (not on connect — a server that accepts and then drops us must still
                    // back off and keep its logging throttled).
                    Interlocked.Exchange(ref _backoffMs, 1000);
                    Interlocked.Exchange(ref _consecutiveFailures, 0);

                    if (!string.IsNullOrEmpty(reply.Error))
                    {
                        long n = Interlocked.Increment(ref _replyErrors);
                        if (n == 1 || n % 100 == 0) _log("HyperHdrReplyError", $"{reply.Error} (#{n})");
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    if (generation != _generation) return;
                    bool closedRightAway = _replied == 0 && Environment.TickCount64 - _connectedAtTicks < 2000;
                    _dropReason = closedRightAway
                        ? "closed by the server right after connecting — if the TV is in another subnet, allow its IP in HyperHDR's network settings"
                        : $"connection lost: {ex.Message}";
                }
                _wake.Set();
            }
        }

        private static void ReadExactly(Socket socket, byte[] buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = socket.Receive(buffer, read, buffer.Length - read, SocketFlags.None);
                if (n == 0) throw new SocketException((int)SocketError.ConnectionReset);
                read += n;
            }
        }

        private void Drop(string reason)
        {
            lock (_stateLock) _generation++;   // silences the old reader
            CloseSocket();
            ScheduleReconnect(reason);
        }

        private void ScheduleReconnect(string reason)
        {
            int backoff = Volatile.Read(ref _backoffMs);
            Interlocked.Exchange(ref _nextConnectTicks, Environment.TickCount64 + backoff);
            Interlocked.Exchange(ref _backoffMs, Math.Min(backoff * 2, MaxBackoffMs));
            // While the server stays down this repeats every 5 s — log the first failure
            // and then about once a minute.
            int n = Interlocked.Increment(ref _consecutiveFailures);
            if (n == 1 || n % 12 == 0)
                _log("HyperHdrDisconnected", $"{Endpoint}: {reason}; retry in {backoff} ms (failure #{n})");
        }

        private void CloseSocket()
        {
            var socket = Interlocked.Exchange(ref _socket, null);
            if (socket == null) return;
            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            socket.Dispose();
        }
    }
}
