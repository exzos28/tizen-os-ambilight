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
    //   { "request": "command", "command": "pause" | "resume" | "rediscover", "id"?: "<id>" }
    //   { "request": "command", "command": "set-outputs", "outputs": "<JSON>", "id"?: "<id>" }
    //       → { "result": "{\"ok\": bool, \"error\": string|null}", "id"?: "<id>" }   (ServiceCommands)
    //
    // Other requests get no reply. Not a trusted port: nothing here is secret or harmful, and
    // an untrusted port works no matter how the package was signed.
    sealed class StatusPort : IDisposable
    {
        public const string PortName = "ambilight.status";

        private readonly MessagePort _port;
        private readonly Func<string> _buildStatus;
        private readonly ICommandTarget _commands;

        private StatusPort(Func<string> buildStatus, ICommandTarget commands)
        {
            _buildStatus = buildStatus;
            _commands = commands;
            _port = new MessagePort(PortName, false);
            _port.MessageReceived += OnMessage;
            _port.Listen();
        }

        // Must be called on the main thread (messages are delivered on its loop, and commands
        // run there). Returns null if the port can't be opened — the service then runs
        // without the panel.
        public static StatusPort TryStart(Func<string> buildStatus, ICommandTarget commands, Action<string, string> log)
        {
            try { return new StatusPort(buildStatus, commands); }
            catch (Exception ex)
            {
                log("StatusPortError", ex.Message);
                return null;
            }
        }

        // A bundle value, or null if it is missing or not a string.
        private static string Item(Bundle bundle, string key) => bundle.TryGetItem(key, out string value) ? value : null;

        private void OnMessage(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                var remote = e.Remote;
                if (remote == null || string.IsNullOrEmpty(remote.AppId) || string.IsNullOrEmpty(remote.PortName)) return;
                string request = Item(e.Message, "request");

                using var reply = new Bundle();
                if (request == "status")
                {
                    reply.AddItem("status", _buildStatus());
                }
                else if (request == "command")
                {
                    var (result, id) = ServiceCommands.Handle(Item(e.Message, "id"), Item(e.Message, "command"), Item(e.Message, "outputs"), _commands);
                    reply.AddItem("result", result);
                    if (id != null) reply.AddItem("id", id);
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
