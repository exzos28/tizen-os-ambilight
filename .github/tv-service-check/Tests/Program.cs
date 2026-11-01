// Tests for the Tizen-independent parts of tv/service. No test framework on purpose:
// it runs as a plain console app in CI.
//
//   dotnet run                          unit tests; writes FlatBuffers samples to ./out
//   dotnet run -- -e2e HOST PORT HTTP   live session against a running HyperHDR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using Google.FlatBuffers;

namespace Service
{
    static class Program
    {
        private static int _failures;

        static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "-e2e")
                return EndToEnd(args[1], int.Parse(args[2]), int.Parse(args[3]));

            LazyLoadTests();   // first: before anything else touches Google.FlatBuffers
            EdgeImageTests();
            ProtocolTests();
            ConfigTests();
            StatusTests();
            ServiceStatusTests();
            CommandTests();
            CaptureLoopTests();
            WriteSamples("out");

            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : $"{_failures} FAILURE(S)");
            return _failures == 0 ? 0 : 1;
        }

        private static void Check(bool ok, string what)
        {
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}");
            if (!ok) _failures++;
        }

        // ---------------------------------------------------------------- Lazy loading

        private static bool IsLoaded(string assemblyName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == assemblyName) return true;
            return false;
        }

        // The service must not load Google.FlatBuffers unless HyperHDR is enabled: config
        // parsing (which validates the HyperHDR section), the status JSON with HyperHDR off and
        // the edge image must all work without it.
        private static void LazyLoadTests()
        {
            var cfg = OutputConfig.Parse(@"{ ""hyperhdr"": { ""enabled"": true, ""host"": ""10.0.0.5"", ""priority"": 100 } }");
            var status = new CaptureStatus();
            status.Frame(DistinctFrame(), 140, 14, 0, 0);
            StatusJson.Build(w => StatusJson.WriteService(w, "1.0.0", status, null, null, cfg.Warnings));
            EdgeImage.Render(DistinctFrame(), 16, 9);
            Check(!IsLoaded("Google.FlatBuffers"), "config, status JSON and edge image don't load Google.FlatBuffers");
        }

        // ---------------------------------------------------------------- EdgeImage

        // h=4, v=3; every sample gets a unique color so orientation mistakes show up.
        private static EdgeFrame DistinctFrame()
        {
            int h = 4, v = 3, n = 2 * (h + v);
            var rgb = new byte[n * 3];
            for (int i = 0; i < n; i++) { rgb[i * 3] = (byte)(i * 18); rgb[i * 3 + 1] = (byte)(255 - i * 18); rgb[i * 3 + 2] = (byte)(i * 7); }
            return new EdgeFrame(h, v, rgb);
        }

        private static byte[] Px(byte[] img, int w, int x, int y) => new[] { img[(y * w + x) * 3], img[(y * w + x) * 3 + 1], img[(y * w + x) * 3 + 2] };
        private static byte[] Sample(EdgeFrame f, int offset, int i) => new[] { f.Rgb[offset + i * 3], f.Rgb[offset + i * 3 + 1], f.Rgb[offset + i * 3 + 2] };
        private static bool Same(byte[] a, byte[] b) => a[0] == b[0] && a[1] == b[1] && a[2] == b[2];
        private static int Dist(byte[] a, byte[] b) => Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) + Math.Abs(a[2] - b[2]);

        // The sample (by flat index) whose color is closest to the pixel.
        private static int Closest(EdgeFrame f, byte[] px)
        {
            int best = -1, bestD = int.MaxValue, n = 2 * (f.H + f.V);
            for (int i = 0; i < n; i++)
            {
                int d = Dist(px, new[] { f.Rgb[i * 3], f.Rgb[i * 3 + 1], f.Rgb[i * 3 + 2] });
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static void EdgeImageTests()
        {
            var f = DistinctFrame();
            int w = 64, h = 36;
            var img = EdgeImage.Render(f, w, h);
            Check(img.Length == w * h * 3, "image size is w*h*3");

            // Corners: the first/last sample of an edge sits exactly on its corner.
            Check(Same(Px(img, w, 0, 0), Sample(f, f.TopOffset, 0)), "top-left pixel = first top sample");
            Check(Same(Px(img, w, w - 1, 0), Sample(f, f.TopOffset, f.H - 1)), "top-right pixel = last top sample");
            Check(Same(Px(img, w, 0, h - 1), Sample(f, f.BottomOffset, f.H - 1)), "bottom-left pixel = last bottom sample (bottom runs right → left)");

            // Orientation along each edge (flat sample indices: top 0-3, right 4-6, bottom 7-10, left 11-13).
            Check(Closest(f, Px(img, w, w - 1, 1)) == 4, "right edge near the top ≈ first right sample");
            Check(Closest(f, Px(img, w, w - 1, h - 2)) == 6, "right edge near the bottom ≈ last right sample");
            Check(Closest(f, Px(img, w, 1, h - 1)) == 10, "bottom edge near the left ≈ last bottom sample");
            Check(Closest(f, Px(img, w, w - 2, h - 1)) == 7, "bottom edge near the right ≈ first bottom sample");
            Check(Closest(f, Px(img, w, 0, h - 2)) == 11, "left edge near the bottom ≈ first left sample");
            Check(Closest(f, Px(img, w, 0, 1)) == 13, "left edge near the top ≈ last left sample");

            // Linear interpolation: halfway between the 2nd and 3rd top samples.
            var mid = Px(img, w, (w - 1) / 2, 0);
            var a = Sample(f, f.TopOffset, 1); var b = Sample(f, f.TopOffset, 2);
            bool between = true;
            for (int c = 0; c < 3; c++) between &= mid[c] >= Math.Min(a[c], b[c]) && mid[c] <= Math.Max(a[c], b[c]);
            Check(between, "top-centre pixel is interpolated between the two middle top samples");

            // Rendering into a reused buffer gives the same image, whatever was in it before.
            var reused = new byte[w * h * 3];
            for (int i = 0; i < reused.Length; i++) reused[i] = 0xAB;
            Check(ReferenceEquals(EdgeImage.Render(f, w, h, reused), reused) && reused.AsSpan().SequenceEqual(img),
                "rendering into a reused buffer overwrites it with the same image");

            // Uniform frame → uniform image.
            var red = new byte[2 * (4 + 3) * 3];
            for (int i = 0; i < red.Length; i += 3) red[i] = 200;
            var u = EdgeImage.Render(new EdgeFrame(4, 3, red), 32, 18);
            bool uniform = true;
            for (int i = 0; i < u.Length; i += 3) uniform &= u[i] == 200 && u[i + 1] == 0 && u[i + 2] == 0;
            Check(uniform, "uniform frame renders a uniform image");

            // Single sample per edge.
            var one = new EdgeFrame(1, 1, new byte[] { 10, 0, 0, 0, 20, 0, 0, 0, 30, 40, 0, 0 });
            var o = EdgeImage.Render(one, 16, 16);
            Check(Same(Px(o, 16, 8, 0), new byte[] { 10, 0, 0 }) && Same(Px(o, 16, 15, 8), new byte[] { 0, 20, 0 })
                  && Same(Px(o, 16, 8, 15), new byte[] { 0, 0, 30 }) && Same(Px(o, 16, 0, 8), new byte[] { 40, 0, 0 }),
                  "one sample per edge fills each edge with that sample");
        }

        // ---------------------------------------------------------------- Protocol

        // Reads a Request with a hand-written table walk (separate from flatc, which CI also runs).
        private static (byte type, Table cmd) ReadRequest(byte[] buf)
        {
            var bb = new ByteBuffer(buf);
            var req = new Table(bb.GetInt(0), bb);
            int o = req.__offset(4);
            byte type = o != 0 ? bb.Get(req.bb_pos + o) : (byte)0;
            o = req.__offset(6);
            return (type, new Table(req.__indirect(req.bb_pos + o), bb));
        }

        private static void ProtocolTests()
        {
            var reg = HyperHdrProtocol.EncodeRegister("Tizen Ambilight", 150);
            var (type, cmd) = ReadRequest(reg);
            Check(type == 4, "Register request has command_type 4");
            int o = cmd.__offset(4);
            Check(o != 0 && cmd.__string(cmd.bb_pos + o) == "Tizen Ambilight", "Register origin round-trips");
            o = cmd.__offset(6);
            Check(o != 0 && cmd.bb.GetInt(cmd.bb_pos + o) == 150, "Register priority round-trips");

            var rgb = new byte[8 * 8 * 3];
            for (int i = 0; i < rgb.Length; i++) rgb[i] = (byte)i;
            var img = HyperHdrProtocol.EncodeRawImage(rgb, 8, 8);
            (type, cmd) = ReadRequest(img);
            Check(type == 2, "Image request has command_type 2");
            o = cmd.__offset(4);
            Check(o != 0 && cmd.bb.Get(cmd.bb_pos + o) == 1, "Image data_type is RawImage (1)");
            Check(cmd.__offset(8) == 0, "Image duration left at default (-1)");
            var raw = new Table(cmd.__indirect(cmd.bb_pos + cmd.__offset(6)), cmd.bb);
            Check(raw.bb.GetInt(raw.bb_pos + raw.__offset(6)) == 8 && raw.bb.GetInt(raw.bb_pos + raw.__offset(8)) == 8, "RawImage width/height round-trip");
            var data = raw.__vector_as_array<byte>(4);
            Check(data != null && data.Length == rgb.Length && data[5] == 5 && data[rgb.Length - 1] == (byte)(rgb.Length - 1), "RawImage data round-trips");

            var framed = HyperHdrProtocol.Frame(new byte[0x010203]);
            Check(framed[0] == 0 && framed[1] == 1 && framed[2] == 2 && framed[3] == 3 && framed.Length == 4 + 0x010203, "length prefix is 4-byte big-endian");
            Check(HyperHdrProtocol.ReadLength(new byte[] { 0, 0, 1, 0 }) == 256, "ReadLength parses big-endian");

            // Reply decoding, built the way HyperHDR's reply.fbs lays it out.
            var b = new FlatBufferBuilder(64);
            var err = b.CreateString("bad image");
            b.StartTable(3); b.AddOffset(0, err.Value, 0); b.AddInt(2, 150, -1); b.Finish(b.EndTable());
            var r = HyperHdrProtocol.DecodeReply(b.SizedByteArray());
            Check(r.Error == "bad image" && r.Registered == 150 && r.Video == -1, "Reply decodes error/registered and defaults video to -1");
        }

        // ---------------------------------------------------------------- Config

        private static void ConfigTests()
        {
            var d = OutputConfig.Parse("{}");
            Check(d.Esp32Enabled && !d.HyperHdr.Enabled && d.Warnings.Count == 0, "empty config = ESP32 only, no warnings");

            var shipped = OutputConfig.Load(Path.Combine(AppContext.BaseDirectory, "../../../../../../tv/service/res/ambilight.json"));
            Check(shipped.Esp32Enabled && !shipped.HyperHdr.Enabled && shipped.Warnings.Count == 0, "shipped res/ambilight.json parses cleanly (comments allowed)");

            var ok = OutputConfig.Parse(@"{ ""hyperhdr"": { ""enabled"": true, ""host"": "" 192.168.1.50 "", ""priority"": 100 } }");
            Check(ok.HyperHdr.Enabled && ok.HyperHdr.Host == "192.168.1.50" && ok.HyperHdr.Priority == 100 && ok.HyperHdr.Port == 19400, "valid HyperHDR section is enabled, host trimmed, defaults kept");

            var noHost = OutputConfig.Parse(@"{ ""hyperhdr"": { ""enabled"": true } }");
            Check(!noHost.HyperHdr.Enabled && noHost.Warnings.Count > 0, "enabled without host → disabled with a warning");

            var badPrio = OutputConfig.Parse(@"{ ""hyperhdr"": { ""enabled"": true, ""host"": ""h"", ""priority"": 300 } }");
            Check(!badPrio.HyperHdr.Enabled && badPrio.Warnings.Exists(w => w.Contains("priority")), "priority outside 50..250 → disabled with a warning");

            var badJson = OutputConfig.Parse("{ not json");
            Check(badJson.Esp32Enabled && !badJson.HyperHdr.Enabled && badJson.Warnings.Count == 1, "invalid JSON → defaults with a warning");

            var off = OutputConfig.Parse(@"{ ""esp32"": { ""enabled"": false } }");
            Check(!off.Esp32Enabled, "esp32.enabled=false is honoured");

            var notObject = OutputConfig.Parse(@"{ ""esp32"": false }");
            Check(notObject.Esp32Enabled && notObject.Warnings.Exists(w => w.Contains("esp32 must be an object")), "\"esp32\": false (not an object) → warning, ESP32 left on");

            var missing = OutputConfig.Load("/nonexistent/ambilight.json");
            Check(missing.Esp32Enabled && missing.Warnings.Count == 1, "missing file → defaults with a warning");
        }

        // ---------------------------------------------------------------- Status panel JSON

        // The launcher (tv/ui/main.js) reads these field names; keep them in sync.
        private static void StatusTests()
        {
            var status = new CaptureStatus();
            JsonElement Json() => JsonDocument.Parse(StatusJson.Build(status.WriteJson)).RootElement;

            var j = Json();
            Check(j.GetProperty("state").GetString() == "waiting" && j.GetProperty("failure").ValueKind == JsonValueKind.Null,
                "status before capture starts: waiting, no failure");

            status.Starting();
            status.Failed(CaptureFailure.Library, "libvideoenhance.so: \"not found\"");
            status.RetryIn(5000);
            j = Json();
            Check(j.GetProperty("state").GetString() == "failed" && j.GetProperty("failure").GetString() == "library", "library failure → state failed, failure library");
            Check(j.GetProperty("error").GetString() == "libvideoenhance.so: \"not found\"", "error text survives JSON escaping");
            Check(j.GetProperty("retryInMs").GetInt64() is > 4000 and <= 5000, "retryInMs counts down from the scheduled retry");
            Check(!j.TryGetProperty("hw", out _) && !j.TryGetProperty("rgb", out _), "no hw / colors before the hardware answered");

            status.Starting();
            status.Hardware(0, 2, 16, 16, 3840, 2160, 20, 20);
            j = Json();
            Check(j.GetProperty("state").GetString() == "starting" && j.GetProperty("failure").ValueKind == JsonValueKind.Null
                  && !j.TryGetProperty("retryInMs", out _), "restart clears the previous failure");
            Check(j.GetProperty("hw").GetProperty("fullW").GetInt32() == 3840 && j.GetProperty("hw").GetProperty("blocks").GetInt32() == 2, "hw condition is reported");

            var frame = DistinctFrame();
            status.Frame(frame, 140, 14, 3, -1);
            j = Json();
            Check(j.GetProperty("state").GetString() == "running" && j.GetProperty("frames").GetInt64() == 1, "first frame → running");
            Check(j.GetProperty("frameMs").GetInt32() == 140 && j.GetProperty("badReads").GetInt32() == 3 && j.GetProperty("lastBadRet").GetInt32() == -1, "frame time and failed reads are reported");
            string rgb = j.GetProperty("rgb").GetString();
            Check(rgb.Length == frame.Rgb.Length * 2 && rgb.Substring(6, 6) == $"{frame.Rgb[3]:X2}{frame.Rgb[4]:X2}{frame.Rgb[5]:X2}", "colors are hex in EdgeFrame order");
            Check(j.GetProperty("h").GetInt32() == 4 && j.GetProperty("v").GetInt32() == 3, "edge counts are reported");

            status.Frame(frame, 140, 14, 0, 0);
            Check(Json().GetProperty("lastBadRet").GetInt32() == -1, "lastBadRet keeps the last error code after a clean frame");

            status.SetPaused(true);
            j = Json();
            Check(j.GetProperty("state").GetString() == "paused" && j.GetProperty("frames").GetInt64() == 2, "paused → state paused, last frame kept");
            status.Frame(frame, 140, 14, 0, 0);   // a frame still in flight when the pause came
            Check(Json().GetProperty("state").GetString() == "paused", "a late frame doesn't end the pause");
            status.Starting();
            status.Failed(CaptureFailure.Hardware, "invalid HW condition");
            status.RetryIn(5000);
            j = Json();
            Check(j.GetProperty("state").GetString() == "paused" && !j.TryGetProperty("retryInMs", out _), "paused hides a failure's retry countdown (no retries while paused)");
            status.SetPaused(false);
            j = Json();
            Check(j.GetProperty("state").GetString() == "failed" && j.TryGetProperty("retryInMs", out _), "unpaused → the real state again");

            var hh = new HyperHdrOutput(OutputConfig.Parse(@"{ ""hyperhdr"": { ""enabled"": true, ""host"": ""10.0.0.5"" } }").HyperHdr, (e, d) => { });
            var hj = JsonDocument.Parse(StatusJson.Build(hh.WriteStatus)).RootElement;
            Check(hj.GetProperty("enabled").GetBoolean() && hj.GetProperty("endpoint").GetString() == "10.0.0.5:19400"
                  && !hj.GetProperty("connected").GetBoolean() && hj.GetProperty("lastError").ValueKind == JsonValueKind.Null,
                "HyperHDR status before connecting");
        }

        // ---------------------------------------------------------------- Whole status reply

        private static JsonElement ServiceJson(string version, CaptureStatus capture, Action<Utf8JsonWriter> esp32, Action<Utf8JsonWriter> hyperHdr, List<string> warnings) =>
            JsonDocument.Parse(StatusJson.Build(w => StatusJson.WriteService(w, version, capture, esp32, hyperHdr, warnings))).RootElement;

        private static void ServiceStatusTests()
        {
            var capture = new CaptureStatus();
            capture.SetPaused(true);
            var j = ServiceJson("1.2.3", capture, null, null, new List<string> { "hyperhdr.host must be a string" });
            Check(j.GetProperty("version").GetString() == "1.2.3", "status carries the package version");
            Check(j.GetProperty("capture").GetProperty("state").GetString() == "paused", "status capture.state is paused");
            Check(!j.GetProperty("esp32").GetProperty("enabled").GetBoolean() && !j.GetProperty("hyperhdr").GetProperty("enabled").GetBoolean(),
                "disabled outputs report enabled: false");
            var warnings = j.GetProperty("configWarnings");
            Check(warnings.GetArrayLength() == 1 && warnings[0].GetString() == "hyperhdr.host must be a string", "config warnings are listed");

            j = ServiceJson(null, new CaptureStatus(), null, null, new List<string>());
            Check(j.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Null, "unknown version is null, not missing");
            Check(j.GetProperty("capture").GetProperty("state").GetString() == "waiting", "capture.state starts as waiting");

            var hh = new HyperHdrOutput(OutputConfig.Parse(@"{ ""hyperhdr"": { ""enabled"": true, ""host"": ""10.0.0.5"" } }").HyperHdr, (e, d) => { });
            j = ServiceJson("1.2.3", new CaptureStatus(), null, hh.WriteStatus, new List<string>());
            Check(j.GetProperty("hyperhdr").GetProperty("endpoint").GetString() == "10.0.0.5:19400", "an enabled output writes its own section");
        }

        // ---------------------------------------------------------------- Launcher commands

        private sealed class FakeTarget : ICommandTarget
        {
            public readonly List<string> Calls = new List<string>();
            public Exception Throw;
            private void Call(string name) { if (Throw != null) throw Throw; Calls.Add(name); }
            public void Pause() => Call("pause");
            public void Resume() => Call("resume");
            public void Rediscover() => Call("rediscover");
        }

        private static (bool ok, JsonElement error) Result(string json)
        {
            var r = JsonDocument.Parse(json).RootElement;
            return (r.GetProperty("ok").GetBoolean(), r.GetProperty("error"));
        }

        private static void CommandTests()
        {
            foreach (var name in new[] { "pause", "resume", "rediscover" })
            {
                var t = new FakeTarget();
                var (ok, error) = Result(ServiceCommands.Execute(name, t));
                Check(ok && error.ValueKind == JsonValueKind.Null && t.Calls.Count == 1 && t.Calls[0] == name, $"command {name} → ok, error null, dispatched once");
            }

            var target = new FakeTarget();
            var r = Result(ServiceCommands.Execute("reboot", target));
            Check(!r.ok && r.error.GetString().Contains("reboot") && target.Calls.Count == 0, "unknown command → ok false with the name in the error, nothing called");
            r = Result(ServiceCommands.Execute("PAUSE", target));
            Check(!r.ok && target.Calls.Count == 0, "command names are case-sensitive");
            r = Result(ServiceCommands.Execute(null, target));
            Check(!r.ok && r.error.GetString().Contains("missing"), "missing command → ok false");
            r = Result(ServiceCommands.Execute("", target));
            Check(!r.ok && r.error.GetString().Contains("missing"), "empty command → ok false");

            target.Throw = new InvalidOperationException("capture is not set up");
            r = Result(ServiceCommands.Execute("pause", target));
            Check(!r.ok && r.error.GetString().Contains("capture is not set up"), "a failing command → ok false with the reason");

            Check(ServiceCommands.ResultJson(null) == "{\"ok\":true,\"error\":null}", "result JSON has exactly ok and error");
        }

        // ---------------------------------------------------------------- Capture loop

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
        {
            long deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(10);
            }
            return condition();
        }

        private static void CaptureLoopTests()
        {
            var logs = new List<string>();
            Action<string, string> log = (e, d) => { lock (logs) logs.Add(e); };

            // A fake capture: runs until cancelled, or fails right away while fail != 0.
            var status = new CaptureStatus();
            string State() => JsonDocument.Parse(StatusJson.Build(status.WriteJson)).RootElement.GetProperty("state").GetString();
            int runs = 0, active = 0, fail = 0;
            CaptureLoop.RunCapture run = token =>
            {
                Interlocked.Increment(ref runs);
                status.Starting();
                if (Volatile.Read(ref fail) != 0) { status.Failed(CaptureFailure.Library, "no library"); return false; }
                Interlocked.Increment(ref active);
                token.WaitHandle.WaitOne();
                Interlocked.Decrement(ref active);
                return true;
            };

            using (var loop = new CaptureLoop(run, status, log, 50, new[] { 100 }))
            {
                loop.Start();
                Check(WaitFor(() => Volatile.Read(ref active) == 1) && Volatile.Read(ref runs) == 1, "capture starts after the start delay");

                loop.Pause();
                Check(loop.IsPaused && State() == "paused", "pause → state paused right away");
                Check(WaitFor(() => Volatile.Read(ref active) == 0), "pause stops the running capture");
                Thread.Sleep(300);
                Check(Volatile.Read(ref runs) == 1, "paused capture is not started again");

                loop.Restart();
                Check(WaitFor(() => Volatile.Read(ref active) == 1) && Volatile.Read(ref runs) == 2 && !loop.IsPaused && State() == "starting",
                    "resume starts capture again right away");

                Volatile.Write(ref fail, 1);
                loop.Restart();   // re-init: this run fails, then retries every 100 ms
                Check(WaitFor(() => Volatile.Read(ref runs) >= 5), "a failed capture is retried after the backoff");
                lock (logs) Check(logs.Contains("CaptureRetry"), "retries are logged");

                loop.Pause();
                Thread.Sleep(50);   // a retry that was already running finishes
                int pausedRuns = Volatile.Read(ref runs);
                Thread.Sleep(400);
                Check(Volatile.Read(ref runs) == pausedRuns && State() == "paused", "no retries while paused");

                Volatile.Write(ref fail, 0);
                loop.Restart();
                Check(WaitFor(() => Volatile.Read(ref active) == 1), "resume after failures starts capture");
            }
            Check(WaitFor(() => Volatile.Read(ref active) == 0), "Dispose stops capture");
            int disposedRuns = Volatile.Read(ref runs);
            Thread.Sleep(200);
            Check(Volatile.Read(ref runs) == disposedRuns, "nothing runs after Dispose");

            // Restart (AppControl start / resume) skips a pending retry delay.
            var status2 = new CaptureStatus();
            int runs2 = 0;
            using (var loop = new CaptureLoop(token => { Interlocked.Increment(ref runs2); return false; }, status2, log, 0, new[] { 60_000 }))
            {
                loop.Start();
                Check(WaitFor(() => Volatile.Read(ref runs2) == 1), "first run without start delay");
                Thread.Sleep(100);
                Check(Volatile.Read(ref runs2) == 1, "next retry waits for the backoff");
                loop.Restart();
                Check(WaitFor(() => Volatile.Read(ref runs2) == 2, 1000), "restart skips the retry delay");
            }

            // An exception from capture is treated like a failure: reported and retried.
            var status3 = new CaptureStatus();
            int runs3 = 0;
            using (var loop = new CaptureLoop(token =>
            {
                if (Interlocked.Increment(ref runs3) == 1) throw new InvalidOperationException("boom");
                token.WaitHandle.WaitOne();
                return true;
            }, status3, log, 0, new[] { 50 }))
            {
                loop.Start();
                Check(WaitFor(() => Volatile.Read(ref runs3) == 2), "a capture that throws is retried");
                var j = JsonDocument.Parse(StatusJson.Build(status3.WriteJson)).RootElement;
                Check(j.GetProperty("state").GetString() == "failed" && j.GetProperty("failure").ValueKind == JsonValueKind.Null
                      && j.GetProperty("error").GetString().Contains("boom"), "…and reported as failed with no failure kind");
            }
        }

        // ---------------------------------------------------------------- Samples for flatc

        private static void WriteSamples(string dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "register.bin"), HyperHdrProtocol.EncodeRegister("Tizen Ambilight", 150));
            var rgb = EdgeImage.Render(DistinctFrame(), 16, 9);
            File.WriteAllBytes(Path.Combine(dir, "image.bin"), HyperHdrProtocol.EncodeRawImage(rgb, 16, 9));
            Console.WriteLine($"wrote {dir}/register.bin and {dir}/image.bin");
        }

        // ---------------------------------------------------------------- End to end

        private static int EndToEnd(string host, int port, int httpPort)
        {
            var settings = OutputConfig.Parse($@"{{ ""hyperhdr"": {{ ""enabled"": true, ""host"": ""{host}"", ""port"": {port}, ""priority"": 150 }} }}").HyperHdr;
            var log = new List<string>();
            using var output = new HyperHdrOutput(settings, (e, d) => { lock (log) log.Add($"{e}: {d}"); Console.WriteLine($"[log] {e}: {d}"); });
            output.Start();

            // ~7 fps for 6 s, colors cycling so HyperHDR sees changing input.
            for (int i = 0; i < 42; i++)
            {
                var rgb = new byte[2 * (4 + 3) * 3];
                for (int k = 0; k < rgb.Length; k += 3) { rgb[k] = (byte)(i * 6); rgb[k + 1] = 80; rgb[k + 2] = (byte)(255 - i * 6); }
                output.Offer(new EdgeFrame(4, 3, rgb));
                Thread.Sleep(140);
            }

            Check(output.IsConnected, "still connected after 6 s of streaming");
            Check(output.RepliesReceived >= 30, $"HyperHDR replied to the register + images (replies={output.RepliesReceived})");
            lock (log) Check(!log.Exists(l => l.StartsWith("HyperHdrReplyError")), "no error replies from HyperHDR");

            // Ask HyperHDR whether our source is registered, while still connected.
            string info = "";
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var resp = http.PostAsync($"http://{host}:{httpPort}/json-rpc",
                    new StringContent(@"{""command"":""serverinfo""}", Encoding.UTF8, "application/json")).Result;
                info = resp.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex) { info = "request failed: " + ex.Message; }
            bool listed = info.Contains("Tizen Ambilight");
            Check(listed, "serverinfo lists the 'Tizen Ambilight' source");
            if (!listed) Console.WriteLine(info.Length > 2000 ? info.Substring(0, 2000) : info);

            // Failure handling, driven by shell commands from CI (skipped when not set).
            string pause = Environment.GetEnvironmentVariable("HH_PAUSE");
            string resume = Environment.GetEnvironmentVariable("HH_RESUME");
            string kill = Environment.GetEnvironmentVariable("HH_KILL");
            string start = Environment.GetEnvironmentVariable("HH_START");
            if (pause != null && resume != null && kill != null && start != null)
            {
                // Frozen server: the socket stays open but nothing answers — like a host that
                // lost power. Only the reply-based liveness check can notice this.
                Shell(pause);
                bool dropped = StreamUntil(output, () => !output.IsConnected, 10_000);
                Check(dropped, "frozen server is detected by the liveness check");
                lock (log) Check(log.Exists(l => l.StartsWith("HyperHdrDisconnected") && l.Contains("no reply")), "disconnect reason is 'no reply'");
                Shell(resume);
                Check(StreamUntil(output, () => output.IsConnected && output.RepliesReceived > 3, 20_000), "reconnects and gets replies after the server resumes");

                // Server restart: the connection closes; the output must reconnect on its own.
                Shell(kill);
                Check(StreamUntil(output, () => !output.IsConnected, 10_000), "closed connection is detected");
                Shell(start);
                Check(StreamUntil(output, () => output.IsConnected && output.RepliesReceived > 3, 40_000), "reconnects after the server restarts");
            }

            Console.WriteLine(_failures == 0 ? "E2E PASSED" : $"E2E: {_failures} FAILURE(S)");
            return _failures == 0 ? 0 : 1;
        }

        // Keeps offering frames at ~7 fps until the condition holds or the timeout expires.
        private static bool StreamUntil(HyperHdrOutput output, Func<bool> condition, int timeoutMs)
        {
            long deadline = Environment.TickCount64 + timeoutMs;
            var rgb = new byte[2 * (4 + 3) * 3];
            while (Environment.TickCount64 < deadline)
            {
                if (condition()) return true;
                output.Offer(new EdgeFrame(4, 3, rgb));
                Thread.Sleep(140);
            }
            return condition();
        }

        private static void Shell(string command)
        {
            Console.WriteLine($"$ {command}");
            using var p = System.Diagnostics.Process.Start("bash", new[] { "-c", command });
            p.WaitForExit();
        }
    }
}
