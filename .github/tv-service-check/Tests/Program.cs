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

            EdgeImageTests();
            ProtocolTests();
            ConfigTests();
            WriteSamples("out");

            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : $"{_failures} FAILURE(S)");
            return _failures == 0 ? 0 : 1;
        }

        private static void Check(bool ok, string what)
        {
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}");
            if (!ok) _failures++;
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

            var missing = OutputConfig.Load("/nonexistent/ambilight.json");
            Check(missing.Esp32Enabled && missing.Warnings.Count == 1, "missing file → defaults with a warning");
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

            Console.WriteLine(_failures == 0 ? "E2E PASSED" : $"E2E: {_failures} FAILURE(S)");
            return _failures == 0 ? 0 : 1;
        }
    }
}
