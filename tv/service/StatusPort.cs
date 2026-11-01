using System;
using Tizen.Applications;
using Tizen.Applications.Messages;

namespace Service
{
    // Answers the launcher's (tv/ui) requests over a local Tizen message port. This is IPC
    // between the two apps of the package on the TV — nothing leaves the device. Every
    // request carries the launcher's own port as reply port; the reply goes there.
    //
    //   { "request": "status" }
    //       → { "status": "<JSON>" }   (format: StatusJson.WriteService, CaptureStatus.WriteJson)
    //   { "request": "command", "command": "pause" | "resume" | "rediscover" }
    //       → { "result": "{\"ok\": bool, \"error\": string|null}" }   (ServiceCommands)
    //
    // Other requests get no reply. Not a trusted port: nothing here is secret or harmful, and
    // an untrusted port works no matter how the package was signed.
    sealed class StatusPort : IDisposable
    {
        public const string PortName = "ambilight.status";

        private readonly MessagePort _port;
        private readonly Func<string> _buildStatus;
        private readonly Func<string, string> _runCommand;

        private StatusPort(Func<string> buildStatus, Func<string, string> runCommand)
        {
            _buildStatus = buildStatus;
            _runCommand = runCommand;
            _port = new MessagePort(PortName, false);
            _port.MessageReceived += OnMessage;
            _port.Listen();
        }

        // Must be called on the main thread (messages are delivered on its loop). Returns
        // null if the port can't be opened — the service then runs without the panel.
        // runCommand gets the "command" value (null if missing or not a string) and returns
        // the result JSON.
        public static StatusPort TryStart(Func<string> buildStatus, Func<string, string> runCommand, Action<string, string> log)
        {
            try { return new StatusPort(buildStatus, runCommand); }
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
                if (!e.Message.TryGetItem("request", out string request)) return;

                using var reply = new Bundle();
                if (request == "status")
                {
                    reply.AddItem("status", _buildStatus());
                }
                else if (request == "command")
                {
                    if (!e.Message.TryGetItem("command", out string command)) command = null;
                    reply.AddItem("result", _runCommand(command));
                }
                else return;
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
