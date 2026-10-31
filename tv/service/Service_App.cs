using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

namespace Service
{
    class App : ServiceApplication
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        private volatile bool _stopping;
        private CancellationTokenSource _cts;
        private Discovery _discovery;
        private Esp32Output _esp32;
        private HyperHdrOutput _hyperHdr;
        private volatile bool _restartCapture;

        // Events logged before any log host is known (startup: config, capture init) are
        // kept here and sent once discovery finds one, instead of being dropped.
        private const int MaxPendingLogs = 100;
        private readonly Queue<string> _pendingLogs = new Queue<string>();

        #region Logging

        // Event log: HTTP POST to port 9000 of every discovered host. Only tools/server.py
        // listens there; requests to ESP32s fail silently.
        private void SendLog(string eventName, string extra = null)
        {
            var json = $"{{\"event\":\"{eventName}\",\"app\":\"Service\",\"timestamp\":\"{DateTime.UtcNow:o}\"" +
                       (extra != null ? $",\"data\":{Escape(extra)}" : "") + "}";
            var hosts = _discovery?.Hosts;
            if (hosts == null || hosts.Length == 0)
            {
                lock (_pendingLogs)
                {
                    if (_pendingLogs.Count == MaxPendingLogs) _pendingLogs.Dequeue();
                    _pendingLogs.Enqueue(json);
                }
                return;
            }
            Post(hosts, json);
        }

        private void FlushPendingLogs(string[] hosts)
        {
            if (hosts.Length == 0) return;
            string[] pending;
            lock (_pendingLogs)
            {
                pending = _pendingLogs.ToArray();
                _pendingLogs.Clear();
            }
            foreach (var json in pending) Post(hosts, json);
        }

        private static async void Post(string[] hosts, string json)
        {
            foreach (var host in hosts)
            {
                try
                {
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await _http.PostAsync($"http://{host}:{Config.HttpPort}", content);
                }
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

        #region Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var config = OutputConfig.Load(Path.Combine(DirectoryInfo.Resource, Config.OutputConfigFile));

            // Discovery always runs: it finds ESP32s to stream to and tools/server.py
            // instances to send the event log to. It never blocks capture.
            _discovery = new Discovery(hosts =>
            {
                Console.WriteLine($"[Discovery] hosts={string.Join(",", hosts)}");
                FlushPendingLogs(hosts);
                SendLog("DiscoveryHosts", $"hosts={string.Join(",", hosts)}");
            });
            _discovery.Start();

            if (config.Esp32Enabled) _esp32 = new Esp32Output(_discovery, (e, d) => SendLog(e, d));
            if (config.HyperHdr.Enabled)
            {
                _hyperHdr = new HyperHdrOutput(config.HyperHdr, (e, d) => SendLog(e, d));
                _hyperHdr.Start();
            }

            Task.Run(async () =>
            {
                try { await Task.Delay(2000, token); } catch (OperationCanceledException) { return; }

                // Queued until a log host is known (see SendLog), so these aren't lost.
                SendLog("Config", $"esp32={config.Esp32Enabled} hyperhdr={(config.HyperHdr.Enabled ? _hyperHdr.Endpoint : "off")}");
                foreach (var warning in config.Warnings) SendLog("ConfigWarning", warning);

                int[] backoffMs = { 5_000, 10_000, 30_000 };
                int attempt = 0;
                while (!token.IsCancellationRequested && !_stopping)
                {
                    bool cleanStop = Capture.Run(OnFrame, (evt, data) => SendLog(evt, data), token);
                    if (token.IsCancellationRequested || _stopping) break;
                    if (cleanStop)
                    {
                        if (!_restartCapture) break;
                        _restartCapture = false;   // Start pressed: re-init capture right away
                        attempt = 0;
                        continue;
                    }

                    int delay = backoffMs[Math.Min(attempt, backoffMs.Length - 1)];
                    attempt++;
                    SendLog("CaptureRetry", $"attempt={attempt} retryIn={delay}ms");
                    try { await Task.Delay(delay, token); } catch (OperationCanceledException) { break; }
                }
            });
        }

        // Called on the capture thread for every frame; must not block.
        private void OnFrame(EdgeFrame frame)
        {
            _esp32?.Send(frame);        // UDP, non-blocking
            _hyperHdr?.Offer(frame);    // mailbox; sent on the HyperHDR thread
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            base.OnAppControlReceived(e);
            string command = "unknown";
            try { if (e.ReceivedAppControl.ExtraData.TryGet("command", out string c)) command = c; } catch { }

            if (command == "stop") { _stopping = true; Exit(); }
            else if (command == "start")
            {
                // The service is already running (it starts with the TV, or this launch just
                // created it). Refresh everything instead of waiting for scheduled retries:
                // probe for hosts (e.g. a new controller or server.py), reconnect HyperHDR, and
                // re-initialise capture so it re-reads the hardware condition.
                _discovery?.TriggerNow();
                _hyperHdr?.ReconnectNow();
                _restartCapture = true;
                Capture.Stop();
            }
        }

        protected override void OnTerminate()
        {
            _stopping = true;
            _cts?.Cancel();
            Capture.Stop();
            _hyperHdr?.Dispose();
            _esp32?.Dispose();
            _discovery?.Dispose();
            base.OnTerminate();
        }

        static void Main(string[] args) { new App().Run(args); }

        #endregion
    }
}
