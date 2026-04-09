using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace Service
{
    static class Capture
    {
        #region Native interop

        [DllImport("libdl.so.2")] static extern IntPtr dlopen(string filename, int flags);
        [DllImport("libdl.so.2")] static extern IntPtr dlsym(IntPtr handle, string symbol);
        [DllImport("libdl.so.2")] static extern IntPtr dlerror();
        const int RTLD_NOW = 2;

        static T GetFunc<T>(IntPtr lib, string name) where T : Delegate
        {
            IntPtr p = dlsym(lib, name);
            return p != IntPtr.Zero ? (T)Marshal.GetDelegateForFunctionPointer(p, typeof(T)) : null;
        }

        #endregion

        #region libvideoenhance structs & delegates

        [StructLayout(LayoutKind.Sequential)]
        struct VEPPIRgbMeasureInfo
        {
            public int measureBlockCnt;
            public int measureBlockWidth;
            public int measureBlockHeight;
            public int measureDelay;
            public int measureFullWidth;
            public int measureFullHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct VEPPIRgbMeasure
        {
            public int mean_R;
            public int mean_G;
            public int mean_B;
            public int lowInputLagOnOff;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int GetConditionFunc(out VEPPIRgbMeasureInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int SetPositionFunc(int idx, int x, int y);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int GetPixelFunc(int idx, out VEPPIRgbMeasure info);

        #endregion

        static volatile bool _running;
        static readonly object _runLock = new object();

        // Returns true on clean stop (_running set to false), false on failure.
        // Sends each captured frame to all hosts in serverHosts simultaneously.
        public static bool Run(string[] serverHosts, Action<string, string> log)
        {
            lock (_runLock)
            {
                _running = true;
                log("CaptureStart", $"targets={string.Join(",", serverHosts)} loading libvideoenhance.so");

                var lib = dlopen("libvideoenhance.so", RTLD_NOW);
                if (lib == IntPtr.Zero)
                {
                    string err = Marshal.PtrToStringAnsi(dlerror()) ?? "unknown";
                    log("CaptureError", $"dlopen failed: {err}");
                    return false;
                }

                var getCondition = GetFunc<GetConditionFunc>(lib, "ppi_ve_get_rgb_measure_condition")
                                ?? GetFunc<GetConditionFunc>(lib, "ve_get_rgb_measure_condition");
                var setPosition = GetFunc<SetPositionFunc>(lib, "ppi_ve_set_rgb_measure_position")
                               ?? GetFunc<SetPositionFunc>(lib, "ve_set_rgb_measure_position");
                var getPixel = GetFunc<GetPixelFunc>(lib, "ppi_ve_get_rgb_measure_pixel")
                            ?? GetFunc<GetPixelFunc>(lib, "ve_get_rgb_measure_pixel");

                if (getCondition == null || setPosition == null || getPixel == null)
                {
                    log("CaptureError", "missing symbols");
                    return false;
                }

                int ret = getCondition(out VEPPIRgbMeasureInfo hw);
                log("HwCondition", $"ret={ret} blocks={hw.measureBlockCnt} " +
                    $"blockSize={hw.measureBlockWidth}x{hw.measureBlockHeight} " +
                    $"full={hw.measureFullWidth}x{hw.measureFullHeight} delay={hw.measureDelay}ms");

                if (ret != 0 || hw.measureBlockCnt < 1 || hw.measureFullWidth < 1)
                {
                    log("CaptureError", "invalid HW condition");
                    return false;
                }

                int fullW = hw.measureFullWidth;
                int fullH = hw.measureFullHeight;
                int captW = hw.measureBlockWidth;
                int captH = hw.measureBlockHeight;
                int blocksPerCycle = hw.measureBlockCnt;
                int delay = Config.CaptureDelayMs >= 0 ? Config.CaptureDelayMs : hw.measureDelay;

                int hCount = Config.TargetCaptureW;
                int vCount = Config.TargetCaptureH;
                int totalPoints = (hCount + vCount) * 2;

                var positions = new (int x, int y)[totalPoints];
                int idx = 0;

                for (int i = 0; i < hCount; i++)
                {
                    int x = (i * fullW / hCount) + (fullW / hCount / 2) - captW / 2;
                    positions[idx++] = (Clamp(x, 0, fullW - captW), 0);
                }
                for (int i = 0; i < vCount; i++)
                {
                    int y = (i * fullH / vCount) + (fullH / vCount / 2) - captH / 2;
                    positions[idx++] = (fullW - captW * 2, Clamp(y, 0, fullH - captH));
                }
                for (int i = 0; i < hCount; i++)
                {
                    int x = ((hCount - 1 - i) * fullW / hCount) + (fullW / hCount / 2) - captW / 2;
                    positions[idx++] = (Clamp(x, 0, fullW - captW), fullH - captH);
                }
                for (int i = 0; i < vCount; i++)
                {
                    int y = ((vCount - 1 - i) * fullH / vCount) + (fullH / vCount / 2) - captH / 2;
                    positions[idx++] = (captW, Clamp(y, 0, fullH - captH));
                }

                int numBatches = (totalPoints + blocksPerCycle - 1) / blocksPerCycle;

                log("CaptureReady", $"points={totalPoints} h={hCount} v={vCount} " +
                    $"batches={numBatches} blocksPerCycle={blocksPerCycle} delay={delay}ms");

                using var udp = new UdpClient();
                var targets = new IPEndPoint[serverHosts.Length];
                for (int i = 0; i < serverHosts.Length; i++)
                    targets[i] = new IPEndPoint(IPAddress.Parse(serverHosts[i]), Config.UdpPort);
                byte[] colors = new byte[totalPoints * 3];
                byte[] packet = new byte[2 + totalPoints * 3];
                packet[0] = (byte)hCount;
                packet[1] = (byte)vCount;
                int frameCount = 0;
                int udpFailures = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Prime: set first batch positions
                int firstBatchSize = Math.Min(blocksPerCycle, totalPoints);
                for (int i = 0; i < firstBatchSize; i++)
                {
                    setPosition(i, positions[i].x, positions[i].y);
                }
                if (delay > 0) Thread.Sleep(delay);

                while (_running)
                {
                    long frameStart = sw.ElapsedMilliseconds;

                    for (int batchIdx = 0; batchIdx < numBatches; batchIdx++)
                    {
                        if (!_running) break;

                        int curStart = batchIdx * blocksPerCycle;
                        int curSize = Math.Min(blocksPerCycle, totalPoints - curStart);

                        for (int i = 0; i < curSize; i++)
                        {
                            ret = getPixel(i, out VEPPIRgbMeasure rgb);
                            int ci = (curStart + i) * 3;
                            if (ret == 0)
                            {
                                // HW API returns 10-bit values (0–1023), scale to 8-bit
                                colors[ci]     = (byte)(Clamp(rgb.mean_R, 0, 1023) >> 2);
                                colors[ci + 1] = (byte)(Clamp(rgb.mean_G, 0, 1023) >> 2);
                                colors[ci + 2] = (byte)(Clamp(rgb.mean_B, 0, 1023) >> 2);
                            }
                        }

                        int nextBatchIdx = (batchIdx + 1) % numBatches;
                        int nextStart = nextBatchIdx * blocksPerCycle;
                        int nextSize = Math.Min(blocksPerCycle, totalPoints - nextStart);
                        for (int i = 0; i < nextSize; i++)
                        {
                            setPosition(i, positions[nextStart + i].x, positions[nextStart + i].y);
                        }

                        if (delay > 0) Thread.Sleep(delay);
                    }

                    frameCount++;
                    Buffer.BlockCopy(colors, 0, packet, 2, totalPoints * 3);
                    foreach (var target in targets)
                    {
                        try { udp.Send(packet, packet.Length, target); }
                        catch (Exception ex)
                        {
                            udpFailures++;
                            if (udpFailures % 30 == 1)
                                log("UdpSendError", $"target={target.Address} failures={udpFailures} err={ex.Message}");
                        }
                    }

                    if (frameCount % 200 == 1)
                    {
                        long actualMs = sw.ElapsedMilliseconds - frameStart;
                        log("Frame", $"#{frameCount} {(actualMs > 0 ? 1000 / actualMs : 999)}fps " +
                            $"p0=({colors[0]},{colors[1]},{colors[2]})");

                        int rightOff = hCount * 3;
                        int leftOff  = (2 * hCount + vCount) * 3;
                        log("SideColors",
                            $"right[0]=({colors[rightOff]},{colors[rightOff+1]},{colors[rightOff+2]}) " +
                            $"left[0]=({colors[leftOff]},{colors[leftOff+1]},{colors[leftOff+2]})");
                    }
                }

                return true; // clean stop via Stop()
            }
        }

        public static void Stop() => _running = false;

        static int Clamp(int val, int min, int max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }
    }
}
