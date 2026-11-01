using System;
using Tizen.Applications;
using Tizen.Applications.Messages;

namespace Service
{
    // Answers the launcher's (tv/ui) status requests over a local Tizen message port. This is
    // IPC between the two apps of the package on the TV — nothing leaves the device.
    //
    //   request: { "request": "status" }, sent with the launcher's own port as reply port
    //   reply:   { "status": "<JSON>" } to that port (format: CaptureStatus.WriteJson and
    //            App.BuildStatusJson)
    //
    // Not a trusted port: the status holds nothing secret, and an untrusted port works no
    // matter how the package was signed.
    sealed class StatusPort : IDisposable
    {
        public const string PortName = "ambilight.status";

        private readonly MessagePort _port;
        private readonly Func<string> _buildStatus;

        private StatusPort(Func<string> buildStatus)
        {
            _buildStatus = buildStatus;
            _port = new MessagePort(PortName, false);
            _port.MessageReceived += OnMessage;
            _port.Listen();
        }

        // Must be called on the main thread (messages are delivered on its loop). Returns
        // null if the port can't be opened — the service then runs without the panel.
        public static StatusPort TryStart(Func<string> buildStatus, Action<string, string> log)
        {
            try { return new StatusPort(buildStatus); }
            catch (Exception ex)
            {
                log("StatusPortError", ex.Message);
                return null;
            }
        }

        private void OnMessage(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                var remote = e.Remote;
                if (remote == null || string.IsNullOrEmpty(remote.AppId) || string.IsNullOrEmpty(remote.PortName)) return;
                if (!e.Message.TryGetItem("request", out string request) || request != "status") return;

                var reply = new Bundle();
                reply.AddItem("status", _buildStatus());
                _port.Send(reply, remote.AppId, remote.PortName);
            }
            catch (Exception ex)
            {
                // E.g. the launcher closed between request and reply.
                Console.Error.WriteLine($"[StatusPort] {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { _port.StopListening(); } catch { }
            _port.Dispose();
        }
    }
}
