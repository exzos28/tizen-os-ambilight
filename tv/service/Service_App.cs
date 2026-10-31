using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

namespace Service
{
    class App : ServiceApplication
    {
        private static readonly HttpClient _http = new HttpClient();
        private volatile bool _stopping;
        private string[] _serverHosts;
        private CancellationTokenSource _cts;
        private readonly object _workerLock = new object();
        private DateTime _workerStartedAt = DateTime.MinValue;

        #region Logging

        private async void SendLog(string eventName, string extra = null)
        {
            if (_serverHosts == null || _serverHosts.Length == 0) return;
            var json = $"{{\"event\":\"{eventName}\",\"app\":\"Service\",\"timestamp\":\"{DateTime.UtcNow:o}\"" +
                       (extra != null ? $",\"data\":{Escape(extra)}" : "") + "}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            foreach (var host in _serverHosts)
            {
                try { await _http.PostAsync($"http://{host}:{Config.HttpPort}", content); }
                catch (Exception ex) { Console.Error.WriteLine($"[SendLog] {host}: {ex.Message}"); }
            }
        }

        private static string Escape(string s)
        {
            return "\"" + s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                + "\"";
        }

        #endregion

        #region Discovery

        // Broadcasts the discovery magic and collects ALL responses within
        // Config.DiscoveryTimeoutMs. Returns every unique IP that replied.
        // Never gives up: after DiscoveryMaxRetries quick attempts it keeps probing
        // every DiscoveryRetryDelayMs, so an ESP32 that comes online later (e.g. after
        // a power cut) is still found. Returns null only when cancelled.
        private string[] DiscoverServers(CancellationToken token)
        {
            var magic = Encoding.UTF8.GetBytes(Config.DiscoveryMagic);
            for (int attempt = 1; !_stopping && !token.IsCancellationRequested; attempt++)
            {
                if (attempt > Config.DiscoveryMaxRetries)
                {
                    if (token.WaitHandle.WaitOne(Config.DiscoveryRetryDelayMs)) break;
                }

                try
                {
                    using var udp = new UdpClient();
                    udp.EnableBroadcast = true;
                    udp.Send(magic, magic.Length,
                             new IPEndPoint(IPAddress.Broadcast, Config.DiscoveryPort));

                    // Collect all replies within the timeout window.
                    udp.Client.ReceiveTimeout = Config.DiscoveryTimeoutMs;
                    var found = new List<string>();
                    while (true)
                    {
                        try
                        {
                            var remote = new IPEndPoint(IPAddress.Any, 0);
                            byte[] resp = udp.Receive(ref remote);
                            string reply = Encoding.UTF8.GetString(resp);
                            if (reply.StartsWith(Config.DiscoveryMagic + ":"))
                            {
                                string ip = reply.Substring(Config.DiscoveryMagic.Length + 1).Trim();
                                if (!found.Contains(ip)) found.Add(ip);
                            }
                        }
                        catch (SocketException) { break; } // timeout — no more replies
                    }

                    if (found.Count > 0) return found.ToArray();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Discovery] attempt {attempt}: {ex.Message}");
                }
            }
            return null;
        }

        #endregion

        #region Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();
            StartWorker();
        }

        // (Re)starts discovery + capture. Cancels a running worker first, so a
        // "start" command re-runs discovery and picks up hosts that appeared or
        // changed IP since the last start.
        private void StartWorker()
        {
            CancellationToken token;
            lock (_workerLock)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                token = _cts.Token;
                _workerStartedAt = DateTime.UtcNow;
            }

            Capture.Stop();
            Task.Run(async () =>
            {
                try { await Task.Delay(2000, token); } catch (OperationCanceledException) { return; }
                if (token.IsCancellationRequested || _stopping) return;

                var hosts = DiscoverServers(token);
                if (hosts == null || _stopping || token.IsCancellationRequested) return;
                _serverHosts = hosts;

                SendLog("OnCreate", $"servers={string.Join(",", _serverHosts)}");
                SendLog("CaptureInit", "starting HW RGB capture...");

                try { await Task.Delay(500, token); } catch (OperationCanceledException) { return; }
                if (token.IsCancellationRequested || _stopping) return;

                int[] backoffMs = { 5_000, 10_000, 30_000 };
                int attempt = 0;
                while (!token.IsCancellationRequested && !_stopping)
                {
                    bool cleanStop = Capture.Run(hosts, (evt, data) => SendLog(evt, data), token);
                    if (cleanStop || token.IsCancellationRequested || _stopping) break;

                    int delay = backoffMs[Math.Min(attempt, backoffMs.Length - 1)];
                    attempt++;
                    SendLog("CaptureRetry", $"attempt={attempt} retryIn={delay}ms");
                    try { await Task.Delay(delay, token); } catch (OperationCanceledException) { break; }
                }
            });
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            base.OnAppControlReceived(e);
            string command = "unknown";
            try { if (e.ReceivedAppControl.ExtraData.TryGet("command", out string c)) command = c; } catch { }
            if (command == "stop") { _stopping = true; Exit(); }
            else if (command == "start")
            {
                // The launch that creates the service also delivers this AppControl;
                // only restart a worker that has been running for a while.
                bool justStarted;
                lock (_workerLock) justStarted = DateTime.UtcNow - _workerStartedAt < TimeSpan.FromSeconds(5);
                if (!justStarted) StartWorker();
            }
        }

        protected override void OnTerminate()
        {
            _stopping = true;
            _cts?.Cancel();
            Capture.Stop();
            base.OnTerminate();
        }

        static void Main(string[] args) { new App().Run(args); }

        #endregion
    }
}
