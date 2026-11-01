using System;

namespace Service
{
    // What the launcher's commands act on (implemented by App).
    interface ICommandTarget
    {
        void Pause();        // stop capture until Resume; outputs stay connected
        void Resume();       // re-initialise capture right away
        void Rediscover();   // probe for ESP32s / tools/server.py now

        // The output settings in effect (for the HyperHDR image size, which set-outputs keeps).
        OutputConfig Outputs { get; }
        // Save outputs (Source = Saved) for the next start, then start / stop the outputs to
        // match. Throws if they can't be saved; nothing is changed then.
        void SetOutputs(OutputConfig outputs);
    }

    // Commands the launcher sends over the status message port (StatusPort):
    //
    //   request: { "request": "command", "command": "pause" | "resume" | "rediscover", "id"?: "<id>" }
    //            { "request": "command", "command": "set-outputs", "outputs": "<JSON>", "id"?: "<id>" }
    //   reply:   { "result": "{\"ok\": true|false, \"error\": null | \"<why>\"}", "id"?: "<id>" }
    //
    // The outputs JSON: protocol/schemas/service/outputs.schema.json, validated by
    // OutputConfig.ParseCommand. "id" (optional, up to MaxIdLength characters) is echoed in
    // the reply so the launcher can tell which request a late reply belongs to; a request
    // without one (older launchers) gets a reply without one. Tizen-independent, so the
    // dispatch is unit-tested in CI.
    static class ServiceCommands
    {
        public const string Pause = "pause";
        public const string Resume = "resume";
        public const string Rediscover = "rediscover";
        public const string SetOutputs = "set-outputs";
        public const int MaxIdLength = 64;

        private const string Expected = "pause, resume, rediscover or set-outputs";

        // A whole command request: id, command and outputs are the bundle's values (null if
        // missing or not a string). Returns the reply's values: Result ("result") and Id
        // ("id"; null = leave it out). An id longer than MaxIdLength is not run and not
        // echoed — it answers ok:false.
        public static (string Result, string Id) Handle(string id, string command, string outputs, ICommandTarget target)
        {
            if (id != null && id.Length > MaxIdLength)
                return (ResultJson($"id is longer than {MaxIdLength} characters; the command was not run"), null);
            return (Execute(command, outputs, target), id);
        }

        // Runs the command on target and returns the reply JSON. outputs is the request's
        // "outputs" value (null if missing or not a string); only set-outputs reads it. Never
        // throws: an unknown or missing command, or one that fails, is reported as ok:false
        // with the reason.
        public static string Execute(string command, string outputs, ICommandTarget target)
        {
            string error;
            try { error = Dispatch(command, outputs, target); }
            catch (Exception ex) { error = $"{command} failed: {ex.Message}"; }
            return ResultJson(error);
        }

        // Returns null on success, otherwise the error.
        private static string Dispatch(string command, string outputs, ICommandTarget target)
        {
            switch (command)
            {
                case null:
                case "":
                    return $"missing command (expected a string: {Expected})";
                case Pause: target.Pause(); return null;
                case Resume: target.Resume(); return null;
                case Rediscover: target.Rediscover(); return null;
                case SetOutputs:
                {
                    if (outputs == null) return "set-outputs needs \"outputs\": the output settings as a JSON string";
                    string invalid = OutputConfig.ParseCommand(outputs, target.Outputs?.HyperHdr, out var config);
                    if (invalid != null) return $"invalid outputs: {invalid}";
                    target.SetOutputs(config);
                    return null;
                }
                default: return $"unknown command \"{command}\" (expected {Expected})";
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
