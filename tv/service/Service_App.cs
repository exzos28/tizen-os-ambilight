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
        private string[] DiscoverServers()
        {
            var magic = Encoding.UTF8.GetBytes(Config.DiscoveryMagic);
            for (int attempt = 1; attempt <= Config.DiscoveryMaxRetries && !_stopping; attempt++)
            {
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
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Capture.Stop();
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                if (token.IsCancellationRequested || _stopping) return;

                _serverHosts = DiscoverServers();
                if (_serverHosts == null || _stopping || token.IsCancellationRequested) return;

                SendLog("OnCreate", $"servers={string.Join(",", _serverHosts)}");
                SendLog("CaptureInit", "starting HW RGB capture...");

                await Task.Delay(500);
                if (token.IsCancellationRequested || _stopping) return;

                int[] backoffMs = { 5_000, 10_000, 30_000 };
                int attempt = 0;
                while (!token.IsCancellationRequested && !_stopping)
                {
                    bool cleanStop = Capture.Run(_serverHosts, (evt, data) => SendLog(evt, data));
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
