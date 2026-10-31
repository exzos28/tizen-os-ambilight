using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Service
{
    static class Capture
    {
        #region Native interop

        // Loaded with dlopen(RTLD_NOW) so unresolved dependencies fail here, at load time,
        // instead of aborting the process on the first call (NativeLibrary.Load uses
        // RTLD_LAZY). If it fails, NativeLibrary.Load is used only to get a reliable error
        // message — dlerror() can come back empty when the runtime touches the loader in
        // between, which used to turn every failure into "unknown".
        [DllImport("libdl.so.2")] static extern IntPtr dlopen(string filename, int flags);
        [DllImport("libdl.so.2")] static extern IntPtr dlerror();
        const int RTLD_NOW = 2;
        const string LibraryName = "libvideoenhance.so";

        static IntPtr LoadLibrary(out string error)
        {
            error = null;
            // Call dlerror() once first: binding the P/Invoke stub runs dlopen/dlsym on
            // libdl, which would otherwise reset the error we want to read below.
            dlerror();
            IntPtr lib = dlopen(LibraryName, RTLD_NOW);
            if (lib != IntPtr.Zero) return lib;

            string dlopenError = Marshal.PtrToStringAnsi(dlerror());
            try
            {
                // Lazy load with the runtime's wider search path, only to tell the two
                // failure kinds apart; the library is not used from this handle.
                NativeLibrary.Free(NativeLibrary.Load(LibraryName));
                error = $"dlopen(RTLD_NOW) failed: {dlopenError ?? "no error text"} " +
                        "(a lazy load from the runtime's search path succeeded, so the library exists — " +
                        "an unresolved symbol, or found only outside the system library path)";
            }
            catch (Exception ex) { error = dlopenError ?? ex.Message; }
            return IntPtr.Zero;
        }

        // Returns the first export found among the given names (firmware versions differ:
        // ppi_ve_* vs ve_*), or null.
        static T GetFunc<T>(IntPtr lib, params string[] names) where T : Delegate
        {
            foreach (var name in names)
                if (NativeLibrary.TryGetExport(lib, name, out IntPtr p))
                    return Marshal.GetDelegateForFunctionPointer<T>(p);
            return null;
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

        // Returns true on clean stop (Stop() called or token cancelled), false on failure.
        // Hands every captured frame to onFrame (on this thread); onFrame must not block.
        public static bool Run(Action<EdgeFrame> onFrame, Action<string, string> log, CancellationToken token)
        {
            lock (_runLock)
            {
                _running = true;
                log("CaptureStart", $"loading {LibraryName}");

                IntPtr lib = LoadLibrary(out string loadError);
                if (lib == IntPtr.Zero)
                {
                    log("CaptureError", $"loading {LibraryName} failed: {loadError}");
                    return false;
                }

                var getCondition = GetFunc<GetConditionFunc>(lib, "ppi_ve_get_rgb_measure_condition", "ve_get_rgb_measure_condition");
                var setPosition  = GetFunc<SetPositionFunc>(lib, "ppi_ve_set_rgb_measure_position", "ve_set_rgb_measure_position");
                var getPixel     = GetFunc<GetPixelFunc>(lib, "ppi_ve_get_rgb_measure_pixel", "ve_get_rgb_measure_pixel");

                if (getCondition == null || setPosition == null || getPixel == null)
                {
                    log("CaptureError", "missing symbols:" +
                        (getCondition == null ? " get_rgb_measure_condition" : "") +
                        (setPosition == null ? " set_rgb_measure_position" : "") +
                        (getPixel == null ? " get_rgb_measure_pixel" : ""));
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

                int inset = Config.EdgeInsetPx;

                // Top: L→R (inset from top edge)
                for (int i = 0; i < hCount; i++)
                {
                    int x = (i * fullW / hCount) + (fullW / hCount / 2) - captW / 2;
                    positions[idx++] = (Clamp(x, 0, fullW - captW), Clamp(inset, 0, fullH - captH));
                }
                // Right: T→B (inset from right edge)
                for (int i = 0; i < vCount; i++)
                {
                    int y = (i * fullH / vCount) + (fullH / vCount / 2) - captH / 2;
                    positions[idx++] = (Clamp(fullW - captW - inset, 0, fullW - captW), Clamp(y, 0, fullH - captH));
                }
                // Bottom: R→L (inset from bottom edge)
                for (int i = 0; i < hCount; i++)
                {
                    int x = ((hCount - 1 - i) * fullW / hCount) + (fullW / hCount / 2) - captW / 2;
                    positions[idx++] = (Clamp(x, 0, fullW - captW), Clamp(fullH - captH - inset, 0, fullH - captH));
                }
                // Left: B→T (inset from left edge)
                for (int i = 0; i < vCount; i++)
                {
                    int y = ((vCount - 1 - i) * fullH / vCount) + (fullH / vCount / 2) - captH / 2;
                    positions[idx++] = (Clamp(inset, 0, fullW - captW), Clamp(y, 0, fullH - captH));
                }

                int numBatches = (totalPoints + blocksPerCycle - 1) / blocksPerCycle;

                log("CaptureReady", $"points={totalPoints} h={hCount} v={vCount} " +
                    $"batches={numBatches} blocksPerCycle={blocksPerCycle} delay={delay}ms");

                byte[] colors = new byte[totalPoints * 3];
                int frameCount = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Prime: set first batch positions
                int firstBatchSize = Math.Min(blocksPerCycle, totalPoints);
                for (int i = 0; i < firstBatchSize; i++)
                {
                    setPosition(i, positions[i].x, positions[i].y);
                }
                if (delay > 0) Thread.Sleep(delay);

                while (_running && !token.IsCancellationRequested)
                {
                    long frameStart = sw.ElapsedMilliseconds;

                    for (int batchIdx = 0; batchIdx < numBatches; batchIdx++)
                    {
                        if (!_running || token.IsCancellationRequested) break;

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
                    if (!_running || token.IsCancellationRequested) break;
                    var frame = new EdgeFrame(hCount, vCount, (byte[])colors.Clone());   // outputs may keep it
                    onFrame(frame);

                    if (frameCount % 200 == 1)
                    {
                        long actualMs = sw.ElapsedMilliseconds - frameStart;
                        var c = frame.Rgb;
                        int t = frame.TopOffset, r = frame.RightOffset, l = frame.LeftOffset;
                        log("Frame", $"#{frameCount} {(actualMs > 0 ? 1000 / actualMs : 999)}fps " +
                            $"p0=({c[t]},{c[t + 1]},{c[t + 2]})");
                        log("SideColors",
                            $"right[0]=({c[r]},{c[r + 1]},{c[r + 2]}) " +
                            $"left[0]=({c[l]},{c[l + 1]},{c[l + 2]})");
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
