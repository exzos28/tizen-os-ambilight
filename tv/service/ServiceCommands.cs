using System;

namespace Service
{
    // What the launcher's commands act on (implemented by App).
    interface ICommandTarget
    {
        void Pause();        // stop capture until Resume; outputs stay connected
        void Resume();       // re-initialise capture right away
        void Rediscover();   // probe for ESP32s / tools/server.py now
    }

    // Commands the launcher sends over the status message port (StatusPort):
    //
    //   request: { "request": "command", "command": "pause" | "resume" | "rediscover" }
    //   reply:   { "result": "{\"ok\": true|false, \"error\": null | \"<why>\"}" }
    //
    // Tizen-independent, so the dispatch is unit-tested in CI.
    static class ServiceCommands
    {
        public const string Pause = "pause";
        public const string Resume = "resume";
        public const string Rediscover = "rediscover";

        // Runs the command on target and returns the reply JSON. Never throws: an unknown or
        // missing command, or one that fails, is reported as ok:false with the reason.
        public static string Execute(string command, ICommandTarget target)
        {
            string error;
            try { error = Dispatch(command, target); }
            catch (Exception ex) { error = $"{command} failed: {ex.Message}"; }
            return ResultJson(error);
        }

        // Returns null on success, otherwise the error.
        private static string Dispatch(string command, ICommandTarget target)
        {
            switch (command)
            {
                case null:
                case "":
                    return "missing command (expected a string: pause, resume or rediscover)";
                case Pause: target.Pause(); return null;
                case Resume: target.Resume(); return null;
                case Rediscover: target.Rediscover(); return null;
                default: return $"unknown command \"{command}\" (expected pause, resume or rediscover)";
            }
        }

        // {"ok": error == null, "error": error}
        public static string ResultJson(string error) => StatusJson.Build(w =>
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", error == null);
            w.WriteString("error", error);
            w.WriteEndObject();
        });
    }
}
