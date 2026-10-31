using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Service
{
    // Finds ESP32 controllers (and tools/server.py) by UDP broadcast, in the background.
    //
    // Capture does not wait for it: Hosts is simply empty until someone answers. While no
    // host is known it probes back to back for the first DiscoveryMaxRetries attempts, then
    // every DiscoveryRetryDelayMs; once hosts are known it re-probes every
    // DiscoveryRefreshMs, so a controller that changed its IP or came online later is
    // picked up without restarting the service. A host is dropped only after it has missed
    // replies for HostTtlMs, so one lost reply doesn't interrupt streaming.
    sealed class Discovery : IDisposable
    {
        private const int HostTtlMs = 120_000;

        private readonly Action<string[]> _onHostsChanged;
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Dictionary<string, long> _lastSeen = new Dictionary<string, long>();
        private CancellationTokenSource _cts;
        private volatile string[] _hosts = Array.Empty<string>();

        public Discovery(Action<string[]> onHostsChanged)
        {
            _onHostsChanged = onHostsChanged;
        }

        // Snapshot of the hosts that answered recently. Safe to read from any thread.
        public string[] Hosts => _hosts;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(() => Loop(token));
        }

        // Probe now instead of waiting for the next scheduled attempt.
        public void TriggerNow() => _wake.Set();

        public void Dispose()
        {
            _cts?.Cancel();
            _wake.Set();
        }

        private void Loop(CancellationToken token)
        {
            int failedAttempts = 0;
            while (!token.IsCancellationRequested)
            {
                var replies = ProbeOnce(token);
                long now = Environment.TickCount64;
                foreach (var ip in replies) _lastSeen[ip] = now;
                foreach (var stale in _lastSeen.Where(kv => now - kv.Value > HostTtlMs).Select(kv => kv.Key).ToList())
                    _lastSeen.Remove(stale);

                var hosts = _lastSeen.Keys.OrderBy(h => h, StringComparer.Ordinal).ToArray();
                if (!hosts.SequenceEqual(_hosts))
                {
                    _hosts = hosts;
                    _onHostsChanged?.Invoke(hosts);
                }

                int waitMs;
                if (hosts.Length > 0) { failedAttempts = 0; waitMs = Config.DiscoveryRefreshMs; }
                else if (++failedAttempts < Config.DiscoveryMaxRetries) waitMs = 0;   // each probe already waits DiscoveryTimeoutMs
                else waitMs = Config.DiscoveryRetryDelayMs;

                if (waitMs > 0) WaitHandle.WaitAny(new[] { _wake, token.WaitHandle }, waitMs);
            }
        }

        // Broadcasts the discovery magic once and returns every IP that replied within
        // DiscoveryTimeoutMs ("AMBILIGHT_DISCOVER:<ip>").
        private static List<string> ProbeOnce(CancellationToken token)
        {
            var found = new List<string>();
            if (token.IsCancellationRequested) return found;
            try
            {
                var magic = Encoding.UTF8.GetBytes(Config.DiscoveryMagic);
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;
                udp.Send(magic, magic.Length, new IPEndPoint(IPAddress.Broadcast, Config.DiscoveryPort));
                udp.Client.ReceiveTimeout = Config.DiscoveryTimeoutMs;

                long deadline = Environment.TickCount64 + Config.DiscoveryTimeoutMs;
                while (Environment.TickCount64 < deadline && !token.IsCancellationRequested)
                {
                    try
                    {
                        var remote = new IPEndPoint(IPAddress.Any, 0);
                        string reply = Encoding.UTF8.GetString(udp.Receive(ref remote));
                        string prefix = Config.DiscoveryMagic + ":";
                        if (!reply.StartsWith(prefix, StringComparison.Ordinal)) continue;
                        string ip = reply.Substring(prefix.Length).Trim();
                        if (IPAddress.TryParse(ip, out _) && !found.Contains(ip)) found.Add(ip);
                    }
                    catch (SocketException) { break; }   // receive timeout — no more replies
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Discovery] {ex.Message}");
                Thread.Sleep(1000);   // e.g. network down: don't spin
            }
            return found;
        }
    }
}
