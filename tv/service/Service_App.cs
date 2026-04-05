using System;
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
        private string _serverHost;
        private CancellationTokenSource _cts;

        #region Logging

        private async void SendLog(string eventName, string extra = null)
        {
            if (_serverHost == null) return;
            try
            {
                var json = $"{{\"event\":\"{eventName}\",\"app\":\"Service\",\"timestamp\":\"{DateTime.UtcNow:o}\"" +
                           (extra != null ? $",\"data\":{Escape(extra)}" : "") + "}";
                await _http.PostAsync($"http://{_serverHost}:{Config.HttpPort}", new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        private static string Escape(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
        }

        #endregion

        #region Discovery

        private string DiscoverServer()
        {
            var magic = Encoding.UTF8.GetBytes(Config.DiscoveryMagic);
            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                var broadcast = new IPEndPoint(IPAddress.Broadcast, Config.DiscoveryPort);
                for (int attempt = 1; attempt <= Config.DiscoveryMaxRetries && !_stopping; attempt++)
                {
                    try
                    {
                        udp.Send(magic, magic.Length, broadcast);
                        udp.Client.ReceiveTimeout = Config.DiscoveryTimeoutMs;
                        var remote = new IPEndPoint(IPAddress.Any, 0);
                        byte[] resp = udp.Receive(ref remote);
                        string reply = Encoding.UTF8.GetString(resp);
                        if (reply.StartsWith(Config.DiscoveryMagic + ":"))
                            return reply.Substring(Config.DiscoveryMagic.Length + 1);
                    }
                    catch (SocketException) { }
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

                _serverHost = DiscoverServer();
                if (_serverHost == null || _stopping || token.IsCancellationRequested) return;
                SendLog("OnCreate", $"server={_serverHost}");

                SendLog("CaptureInit", "starting HW RGB capture...");

                await Task.Delay(500);
                if (token.IsCancellationRequested || _stopping) return;

                Capture.Run(_serverHost, (evt, data) => SendLog(evt, data));
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
