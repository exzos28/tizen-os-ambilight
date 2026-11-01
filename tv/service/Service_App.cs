using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Tizen.Applications;
using static System.FormattableString;

namespace Service
{
    class App : ServiceApplication, ICommandTarget
    {
        private Discovery _discovery;
        private Esp32Output _esp32;
        private HyperHdrOutput _hyperHdr;
        private OutputConfig _config;
        private CaptureLoop _capture;
        private readonly CaptureStatus _captureStatus = new CaptureStatus();
        private StatusPort _statusPort;
        private string _version;   // package version, null if it could not be read
        private static GCLatencyMode _initialGcLatency;

        // Events logged before any log host is known (startup: config, capture init) are
        // kept here and sent once discovery finds one, instead of being dropped.
        private const int MaxPendingLogs = 100;
        private readonly Queue<string> _pendingLogs = new Queue<string>();

        #region Logging

        // Event log: HTTP POST to port 9000 of every discovered host. Only tools/server.py
        // listens there; requests to ESP32s fail silently.
        private void SendLog(string eventName, string extra = null)
        {
            var json = $"{{\"event\":\"{eventName}\",\"app\":\"Service\",\"timestamp\":\"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}\"" +
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

        // Only called once a host is known, so a TV with no ESP32 or tools/server.py on the
        // network never creates the HttpClient (nor loads System.Net.Http). NoInlining keeps
        // the HTTP types out of the callers.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async void Post(string[] hosts, string json)
        {
            foreach (var host in hosts)
            {
                try
                {
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await EventLogHttp.Client.PostAsync(Invariant($"http://{host}:{Config.HttpPort}"), content);
                }
                catch (Exception ex) { Console.Error.WriteLine($"[SendLog] {host}: {ex.Message}"); }
            }
        }

        // Created on first use, by the (thread-safe) type initializer.
        private static class EventLogHttp
        {
            public static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
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
            _version = ReadVersion();

            var config = _config = OutputConfig.Load(Path.Combine(DirectoryInfo.Resource, Config.OutputConfigFile));

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
            // Google.FlatBuffers is loaded only when HyperHdrProtocol is first used, on the
            // HyperHDR sender thread — so only when HyperHDR is enabled.
            if (config.HyperHdr.Enabled)
            {
                _hyperHdr = new HyperHdrOutput(config.HyperHdr, (e, d) => SendLog(e, d));
                _hyperHdr.Start();
            }

            // The launcher's status panel asks here whether capture and the outputs work, and
            // sends its commands (pause / resume / rediscover).
            _statusPort = StatusPort.TryStart(BuildStatusJson, command => ServiceCommands.Execute(command, this), (e, d) => SendLog(e, d));

            // Queued until a log host is known (see SendLog), so these aren't lost.
            SendLog("Runtime", Invariant($"version={_version ?? "unknown"} dotnet={Environment.Version} serverGc={GCSettings.IsServerGC} gcLatency={_initialGcLatency}->{GCSettings.LatencyMode} culture={CultureInfo.CurrentCulture.Name} heap={GC.GetTotalMemory(false)}"));
            SendLog("Config", $"esp32={config.Esp32Enabled} hyperhdr={(config.HyperHdr.Enabled ? _hyperHdr.Endpoint : "off")}");
            foreach (var warning in config.Warnings) SendLog("ConfigWarning", warning);

            // First run after 2 s; failures retried after 5 s, 10 s, then every 30 s.
            _capture = new CaptureLoop(
                token => Capture.Run(OnFrame, (evt, data) => SendLog(evt, data), _captureStatus, token),
                _captureStatus, (e, d) => SendLog(e, d), 2000, new[] { 5_000, 10_000, 30_000 });
            _capture.Start();
        }

        // The package version from tizen-manifest.xml / config.xml (synced from package.json
        // by scripts/sync-versions.mjs), via app_get_version — needs no privilege.
        private string ReadVersion()
        {
            try
            {
                string version = this.Version;
                if (!string.IsNullOrEmpty(version)) return version;
                SendLog("VersionUnknown", "app_get_version returned no version");
            }
            catch (Exception ex) { SendLog("VersionUnknown", ex.Message); }
            return null;
        }

        // Reply for the launcher's status panel (StatusPort). Runs on the main thread.
        private string BuildStatusJson() => StatusJson.Build(w => StatusJson.WriteService(w, _version, _captureStatus,
            _esp32 != null ? (Action<Utf8JsonWriter>)_esp32.WriteStatus : null,
            _hyperHdr != null ? (Action<Utf8JsonWriter>)_hyperHdr.WriteStatus : null,
            _config.Warnings));

        // Called on the capture thread for every frame; must not block.
        private void OnFrame(EdgeFrame frame)
        {
            _esp32?.Send(frame);        // UDP, non-blocking
            _hyperHdr?.Offer(frame);    // mailbox; sent on the HyperHDR thread
        }

        // Launcher commands (StatusPort → ServiceCommands), on the main thread.
        void ICommandTarget.Pause() => (_capture ?? throw new InvalidOperationException("capture is not set up")).Pause();
        void ICommandTarget.Resume() => (_capture ?? throw new InvalidOperationException("capture is not set up")).Restart();
        void ICommandTarget.Rediscover() => (_discovery ?? throw new InvalidOperationException("discovery is not running")).TriggerNow();

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            base.OnAppControlReceived(e);
            string command = "unknown";
            try { if (e.ReceivedAppControl.ExtraData.TryGet("command", out string c)) command = c; } catch { }

            if (command == "stop") { _capture?.Dispose(); Exit(); }
            else if (command == "start")
            {
                // The service is already running (it starts with the TV, or this launch just
                // created it). Refresh everything instead of waiting for scheduled retries:
                // probe for hosts (e.g. a new controller or server.py), reconnect HyperHDR, and
                // re-initialise capture so it re-reads the hardware condition. Also ends a pause.
                _discovery?.TriggerNow();
                _hyperHdr?.ReconnectNow();
                _capture?.Restart();
            }
        }

        protected override void OnTerminate()
        {
            _capture?.Dispose();
            _statusPort?.Dispose();
            _hyperHdr?.Dispose();
            _esp32?.Dispose();
            _discovery?.Dispose();
            base.OnTerminate();
        }

        static void Main(string[] args)
        {
            // Blocking instead of background (concurrent) full GCs: no background GC thread and
            // no extra memory while one runs; the heap is tiny, so the pauses don't matter.
            // Set here rather than in Service.csproj: Tizen's dotnet-launcher starts the runtime
            // itself and does not read Service.runtimeconfig.json (docs/ARCHITECTURE.md, Memory).
            _initialGcLatency = GCSettings.LatencyMode;
            GCSettings.LatencyMode = GCLatencyMode.Batch;
            new App().Run(args);
        }

        #endregion
    }
}
