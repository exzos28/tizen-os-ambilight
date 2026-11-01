using System;
using System.Threading;
using static System.FormattableString;

namespace Service
{
    // Runs capture (Capture.Run) on its own thread and decides when it runs:
    //
    //   - first run after startDelayMs;
    //   - a failed run is retried after backoffMs[0], [1], … (the last value repeats);
    //   - Restart() (AppControl start, command "resume") re-initialises capture right away:
    //     it stops a running capture, skips a pending retry delay and ends a pause;
    //   - Pause() (command "pause") stops capture and keeps it stopped — no retries — until
    //     Restart(). Outputs are not touched, so they stay connected. The status says "paused".
    //
    // Tizen-independent (the capture itself is passed in), so it is unit-tested in CI.
    sealed class CaptureLoop : IDisposable
    {
        // Runs capture until the token is cancelled (returns true) or capture fails (false).
        public delegate bool RunCapture(CancellationToken token);

        private readonly RunCapture _run;
        private readonly CaptureStatus _status;
        private readonly Action<string, string> _log;
        private readonly int _startDelayMs;
        private readonly int[] _backoffMs;

        // Guards everything below; Monitor.Wait/PulseAll on it wakes the loop.
        private readonly object _lock = new object();
        private CancellationTokenSource _current;   // cancels the running capture, null between runs
        private bool _paused;
        private bool _restart;
        private bool _disposed;
        private Thread _thread;

        public CaptureLoop(RunCapture run, CaptureStatus status, Action<string, string> log, int startDelayMs, int[] backoffMs)
        {
            if (backoffMs == null || backoffMs.Length == 0) throw new ArgumentException("need at least one retry delay", nameof(backoffMs));
            _run = run;
            _status = status;
            _log = log;
            _startDelayMs = startDelayMs;
            _backoffMs = backoffMs;
        }

        public bool IsPaused { get { lock (_lock) return _paused; } }

        public void Start()
        {
            _thread = new Thread(Loop) { IsBackground = true, Name = "Capture" };
            _thread.Start();
        }

        public void Pause()
        {
            lock (_lock)
            {
                _paused = true;
                _restart = false;
                _status.SetPaused(true);
                _current?.Cancel();
                Monitor.PulseAll(_lock);
            }
        }

        public void Restart()
        {
            lock (_lock)
            {
                _paused = false;
                _restart = true;
                _status.SetPaused(false);
                _current?.Cancel();
                Monitor.PulseAll(_lock);
            }
        }

        // Stops capture for good and waits briefly for the capture thread to leave.
        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _current?.Cancel();
                Monitor.PulseAll(_lock);
            }
            var thread = _thread;
            if (thread != null && thread != Thread.CurrentThread) thread.Join(1000);
        }

        private void Loop()
        {
            int attempt = 0;             // failed runs since the last success or Restart()
            int waitMs = _startDelayMs;  // before the next run
            while (true)
            {
                CancellationTokenSource cts;
                lock (_lock)
                {
                    long until = Environment.TickCount64 + waitMs;
                    while (!_disposed)
                    {
                        if (_restart) { _restart = false; attempt = 0; break; }
                        if (_paused) { Monitor.Wait(_lock); continue; }
                        long left = until - Environment.TickCount64;
                        if (left <= 0) break;
                        Monitor.Wait(_lock, (int)Math.Min(left, int.MaxValue));
                    }
                    if (_disposed) return;
                    cts = _current = new CancellationTokenSource();
                }

                bool cleanStop;
                try { cleanStop = _run(cts.Token); }
                catch (Exception ex)
                {
                    // Not one of the known failures (those are reported by Capture.Run itself):
                    // state failed with no failure kind, retried like any failure.
                    _log("CaptureError", $"unexpected: {ex}");
                    _status.Failed(CaptureFailure.None, $"unexpected error: {ex.Message}");
                    cleanStop = false;
                }

                lock (_lock)
                {
                    _current = null;
                    cts.Dispose();
                    if (_disposed) return;
                    // Stopped (or failed) because of Pause() / Restart(): the wait above handles it.
                    if (_paused || _restart) { waitMs = 0; continue; }
                }
                if (cleanStop) return;   // only Pause/Restart/Dispose stop a run cleanly

                int delay = _backoffMs[Math.Min(attempt, _backoffMs.Length - 1)];
                attempt++;
                _status.RetryIn(delay);
                _log("CaptureRetry", Invariant($"attempt={attempt} retryIn={delay}ms"));
                waitMs = delay;
            }
        }
    }
}
