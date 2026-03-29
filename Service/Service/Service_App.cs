using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

namespace Service
{
    class App : ServiceApplication
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string SERVER_HOST = "192.168.50.184";
        private const int HTTP_PORT = 9000;
        private const int UDP_PORT = 9001;
        private const string SERVER = "http://192.168.50.184:9000";
        private const int DOWNSCALE = 10;
        private const int EDGE_DIVISOR = 10;
        private const int MAX_FPS = 60;
        private const int STATS_EVERY = 100;

        private System.Threading.Timer _heartbeat;
        private volatile bool _stopping;

        #region Logging

        private async void SendLog(string eventName, string extra = null)
        {
            try
            {
                var json = $"{{\"event\":\"{eventName}\",\"app\":\"Service\",\"timestamp\":\"{DateTime.UtcNow:o}\"" +
                           (extra != null ? $",\"data\":{Escape(extra)}" : "") + "}";
                await _http.PostAsync(SERVER, new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        private static string Escape(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
        }

        #endregion

        #region Native interop

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        static extern IntPtr DlOpen(string filename, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        static extern IntPtr DlSym(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", EntryPoint = "memset")]
        static extern IntPtr Memset(IntPtr dest, int c, int count);

        const int RTLD_LAZY = 1, RTLD_GLOBAL = 0x100;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr InitFunc(int width, int height);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr TakeFunc(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int SimpleFunc(IntPtr ptr);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int MapFunc(IntPtr surface, int opt, IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int SysInfoFunc(string key, IntPtr valuePtr);

        static T Sym<T>(IntPtr lib, string name) where T : Delegate
        {
            IntPtr p = DlSym(lib, name);
            if (p == IntPtr.Zero) throw new Exception($"Symbol not found: {name}");
            return (T)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
        }

        #endregion

        #region Screen resolution

        private (int w, int h) GetScreenSize()
        {
            int w = 1920, h = 1080;
            try
            {
                IntPtr sysLib = DlOpen("libcapi-system-info.so", RTLD_LAZY);
                if (sysLib == IntPtr.Zero)
                    sysLib = DlOpen("/usr/lib/libcapi-system-info.so.0", RTLD_LAZY);
                if (sysLib == IntPtr.Zero) return (w, h);

                var getInt = Sym<SysInfoFunc>(sysLib, "system_info_get_platform_int");
                IntPtr valPtr = Marshal.AllocHGlobal(4);

                if (getInt("http://tizen.org/feature/screen.width", valPtr) == 0)
                    w = Marshal.ReadInt32(valPtr);
                if (getInt("http://tizen.org/feature/screen.height", valPtr) == 0)
                    h = Marshal.ReadInt32(valPtr);

                Marshal.FreeHGlobal(valPtr);
            }
            catch { }
            return (w, h);
        }

        #endregion

        #region Capture

        private void RunCapture()
        {
            var (screenW, screenH) = GetScreenSize();
            int captureW = screenW / DOWNSCALE;
            int captureH = screenH / DOWNSCALE;
            int hCount = captureW / EDGE_DIVISOR;
            int vCount = captureH / EDGE_DIVISOR;
            int totalLeds = (hCount + vCount) * 2;

            SendLog("Config", $"screen={screenW}x{screenH}, capture={captureW}x{captureH}, edges={hCount}h+{vCount}v={totalLeds} leds");

            IntPtr lib = DlOpen("/usr/lib/libcapi-ui-efl-util.so.0", RTLD_LAZY | RTLD_GLOBAL);
            if (lib == IntPtr.Zero) { SendLog("FATAL", "lib load failed"); return; }

            var ssInit   = Sym<InitFunc>(lib, "efl_util_screenshot_initialize");
            var ssTake   = Sym<TakeFunc>(lib, "efl_util_screenshot_take_tbm_surface");
            var ssDeinit = Sym<SimpleFunc>(lib, "efl_util_screenshot_deinitialize");
            var mapFn    = Sym<MapFunc>(lib, "tbm_surface_map");
            var unmapFn  = Sym<SimpleFunc>(lib, "tbm_surface_unmap");

            // Try to load tbm_surface_internal_unref for freeing surfaces
            SimpleFunc unrefFn = null;
            try { unrefFn = Sym<SimpleFunc>(lib, "tbm_surface_internal_unref"); }
            catch { }
            if (unrefFn == null)
            {
                IntPtr tbmLib = DlOpen("libtbm.so.0", RTLD_LAZY);
                if (tbmLib != IntPtr.Zero)
                    try { unrefFn = Sym<SimpleFunc>(tbmLib, "tbm_surface_internal_unref"); } catch { }
            }
            SendLog("SurfaceFree", unrefFn != null ? "tbm_surface_internal_unref found" : "no unref, surfaces may leak");

            IntPtr ssHandle = ssInit(captureW, captureH);
            if (ssHandle == IntPtr.Zero) { SendLog("FATAL", "init failed"); return; }

            var udp = new UdpClient();
            var endpoint = new IPEndPoint(IPAddress.Parse(SERVER_HOST), UDP_PORT);

            int infoSize = 256;
            IntPtr infoPtr = Marshal.AllocHGlobal(infoSize);

            // Pre-compute pixel offsets (byte offset into native BGRX buffer)
            // Each edge pixel = one offset into the capture buffer
            int[] offsets = new int[totalLeds];
            int oi = 0;

            // Top: left to right, y=0
            for (int i = 0; i < hCount; i++)
                offsets[oi++] = ((i * 2 + 1) * captureW / (hCount * 2)) * 4;

            // Right: top to bottom, x=captureW-1
            for (int i = 0; i < vCount; i++)
                offsets[oi++] = ((i * 2 + 1) * captureH / (vCount * 2) * captureW + captureW - 1) * 4;

            // Bottom: right to left, y=captureH-1
            for (int i = hCount - 1; i >= 0; i--)
                offsets[oi++] = (((captureH - 1) * captureW) + (i * 2 + 1) * captureW / (hCount * 2)) * 4;

            // Left: bottom to top, x=0
            for (int i = vCount - 1; i >= 0; i--)
                offsets[oi++] = ((i * 2 + 1) * captureH / (vCount * 2) * captureW) * 4;

            // Pre-allocate packet: [1B hCount][1B vCount][RGB * totalLeds]
            int packetSize = 2 + totalLeds * 3;
            byte[] packet = new byte[packetSize];
            packet[0] = (byte)hCount;
            packet[1] = (byte)vCount;

            int frameNum = 0;
            var sw = new Stopwatch();
            var frameSw = new Stopwatch();
            long statsCaptureMs = 0, statsProcessMs = 0, statsSendMs = 0;
            int statsFrames = 0;
            var totalSw = Stopwatch.StartNew();
            long minFrameTimeMs = 1000 / MAX_FPS;

            SendLog("CaptureLoop", $"UDP to {SERVER_HOST}:{UDP_PORT}, packet={packetSize}B, maxFps={MAX_FPS}");

            while (!_stopping)
            {
                frameSw.Restart();
                try
                {
                    // --- Capture + read pixels while mapped ---
                    sw.Restart();
                    IntPtr surf = ssTake(ssHandle);
                    if (surf == IntPtr.Zero) continue;

                    Memset(infoPtr, 0, infoSize);
                    if (mapFn(surf, 1, infoPtr) != 0) { if (unrefFn != null) unrefFn(surf); continue; }

                    IntPtr pixPtr = Marshal.ReadIntPtr(infoPtr, 24);
                    if (pixPtr == IntPtr.Zero) { unmapFn(surf); if (unrefFn != null) unrefFn(surf); continue; }

                    // Read edge pixels WHILE surface is still mapped
                    int pi = 2;
                    for (int i = 0; i < totalLeds; i++)
                    {
                        int off = offsets[i];
                        packet[pi++] = Marshal.ReadByte(pixPtr, off + 2); // R
                        packet[pi++] = Marshal.ReadByte(pixPtr, off + 1); // G
                        packet[pi++] = Marshal.ReadByte(pixPtr, off);     // B
                    }

                    unmapFn(surf);
                    if (unrefFn != null) unrefFn(surf);
                    long captureMs = sw.ElapsedMilliseconds;
                    long processMs = 0;

                    // --- Send UDP ---
                    sw.Restart();
                    udp.Send(packet, packetSize, endpoint);
                    long sendMs = sw.ElapsedMilliseconds;

                    frameNum++;
                    statsFrames++;
                    statsCaptureMs += captureMs;
                    statsProcessMs += processMs;
                    statsSendMs += sendMs;

                    if (statsFrames >= STATS_EVERY)
                    {
                        double avgCapture = (double)statsCaptureMs / statsFrames;
                        double avgProcess = (double)statsProcessMs / statsFrames;
                        double avgSend = (double)statsSendMs / statsFrames;
                        double realFps = frameNum / totalSw.Elapsed.TotalSeconds;

                        SendLog("Stats",
                            $"fps={realFps:F1}, capture={avgCapture:F1}ms, process={avgProcess:F1}ms, send={avgSend:F1}ms, frames={frameNum}");

                        statsFrames = 0;
                        statsCaptureMs = 0;
                        statsProcessMs = 0;
                        statsSendMs = 0;
                    }
                }
                catch (Exception ex)
                {
                    SendLog("CaptureError", ex.Message);
                    Thread.Sleep(100);
                }

                // Cap FPS
                long elapsed = frameSw.ElapsedMilliseconds;
                if (elapsed < minFrameTimeMs)
                    Thread.Sleep((int)(minFrameTimeMs - elapsed));
            }

            Marshal.FreeHGlobal(infoPtr);
            udp.Close();
            ssDeinit(ssHandle);
            SendLog("CaptureStopped", $"total frames={frameNum}");
        }

        #endregion

        #region Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();
            SendLog("OnCreate");
            _heartbeat = new System.Threading.Timer(_ => SendLog("heartbeat"), null, 15000, 15000);
            Task.Run(async () => { await Task.Delay(2000); RunCapture(); });
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            base.OnAppControlReceived(e);
            string command = "unknown";
            try { if (e.ReceivedAppControl.ExtraData.TryGet("command", out string c)) command = c; } catch { }
            SendLog("OnAppControlReceived", command);
            if (command == "stop") { _stopping = true; SendLog("ExitRequested"); Exit(); }
        }

        protected override void OnTerminate()
        {
            _stopping = true;
            _heartbeat?.Dispose();
            SendLog("OnTerminate");
            base.OnTerminate();
        }

        static void Main(string[] args) { new App().Run(args); }

        #endregion
    }
}
