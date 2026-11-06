using System;
using System.Runtime.InteropServices;
using static System.FormattableString;

namespace Service
{
    // Private adapter for Samsung's caller-owned NV12 video-capture API. The ABI is only
    // enabled on the ARM32 layout that was verified for Tizen 9; unknown runtimes stay on PPI.
    sealed class VideoCapture : IDisposable
    {
        private const string LibraryName = "/usr/lib/libvideo-capture.so.0.1.0";
        private const string CaptureSymbol = "secvideo_api_capture_video_main";
        private const int RTLD_NOW = 2;
        // Reserve a generous zeroed tail because newer struct fields after the verified pitches
        // (including a timestamp) may be written by the native API.
        private const int ParamSize = 0x100;
        private const int YCapacity = Nv12FrameConverter.MaxWidth * Nv12FrameConverter.MaxHeight;
        private const int UvCapacity = YCapacity / 2;

        [DllImport("libdl.so.2")] private static extern IntPtr dlopen(string filename, int flags);
        [DllImport("libdl.so.2")] private static extern IntPtr dlerror();

        // ARM32 layout derived from the exported function's native parameter structure.
        // Unassigned bytes are reserved and always zero-initialized before the call.
        [StructLayout(LayoutKind.Explicit, Size = ParamSize)]
        private struct CaptureParam
        {
            [FieldOffset(0)] public uint YSize;
            [FieldOffset(4)] public uint UvSize;
            [FieldOffset(8)] public uint Width;
            [FieldOffset(0x0c)] public uint Height;
            [FieldOffset(0x10)] public uint YPointer;
            [FieldOffset(0x14)] public uint UvPointer;
            [FieldOffset(0x28)] public uint Interlaced;
            [FieldOffset(0x2c)] public uint YPitch;
            [FieldOffset(0x30)] public uint UvPitch;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CaptureVideoFunc(int requestWidth, int requestHeight, ref CaptureParam param);

        private IntPtr _library;
        private CaptureVideoFunc _capture;
        private IntPtr _yBuffer;
        private IntPtr _uvBuffer;
        private readonly byte[] _yPlane = new byte[YCapacity];
        private readonly byte[] _uvPlane = new byte[UvCapacity];
        private readonly byte[] _zeroY = new byte[YCapacity];
        private readonly byte[] _zeroUv = new byte[UvCapacity];
        private bool _disposed;

        private VideoCapture(IntPtr library, CaptureVideoFunc capture)
        {
            _library = library;
            _capture = capture;
            try
            {
                _yBuffer = Marshal.AllocHGlobal(YCapacity);
                _uvBuffer = Marshal.AllocHGlobal(UvCapacity);
                if (_yBuffer == IntPtr.Zero || _uvBuffer == IntPtr.Zero)
                    throw new OutOfMemoryException("allocating NV12 planes returned a null pointer");
                Marshal.Copy(_zeroY, 0, _yBuffer, YCapacity);
                Marshal.Copy(_zeroUv, 0, _uvBuffer, UvCapacity);
            }
            catch
            {
                if (_yBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_yBuffer);
                if (_uvBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_uvBuffer);
                _yBuffer = _uvBuffer = IntPtr.Zero;
                throw;
            }
        }

        public static VideoCapture TryOpen(out string error)
        {
            error = null;
            if (IntPtr.Size != 4)
            {
                error = Invariant($"{CaptureSymbol} requires the verified 32-bit ARM ABI; runtime pointer size is {IntPtr.Size}");
                return null;
            }
            if (!HasVerifiedLayout(out error)) return null;

            IntPtr library;
            try
            {
                dlerror();
                library = dlopen(LibraryName, RTLD_NOW);
            }
            catch (Exception ex)
            {
                error = Invariant($"loading {LibraryName} failed: {ex.Message}");
                return null;
            }
            if (library == IntPtr.Zero)
            {
                string nativeError = Marshal.PtrToStringAnsi(dlerror());
                error = Invariant($"loading {LibraryName} failed: {nativeError ?? "no error text"}");
                return null;
            }

            try
            {
                if (!NativeLibrary.TryGetExport(library, CaptureSymbol, out IntPtr symbol))
                {
                    error = Invariant($"{LibraryName} does not export {CaptureSymbol}");
                    NativeLibrary.Free(library);
                    return null;
                }
                var capture = Marshal.GetDelegateForFunctionPointer<CaptureVideoFunc>(symbol);
                return new VideoCapture(library, capture);
            }
            catch (Exception ex)
            {
                NativeLibrary.Free(library);
                error = Invariant($"initializing {CaptureSymbol} failed: {ex.Message}");
                return null;
            }
        }

        public bool TryCapture(out PixelFrame frame, out string error)
        {
            frame = null;
            error = null;
            if (_disposed) return Fail("video capture adapter is disposed", out error);

            try
            {
                // Clear caller-owned planes so a partial native write can never reuse bytes
                // from the preceding frame and appear to be a valid complete image.
                Marshal.Copy(_zeroY, 0, _yBuffer, YCapacity);
                Marshal.Copy(_zeroUv, 0, _uvBuffer, UvCapacity);

                var param = new CaptureParam
                {
                    YSize = YCapacity,
                    UvSize = UvCapacity,
                    YPointer = PointerValue(_yBuffer),
                    UvPointer = PointerValue(_uvBuffer),
                };
                int rc = _capture(Nv12FrameConverter.MaxWidth, Nv12FrameConverter.MaxHeight, ref param);
                if (rc != 0) return Fail(Invariant($"{CaptureSymbol} returned rc={rc}"), out error);
                if (param.YPointer != PointerValue(_yBuffer) || param.UvPointer != PointerValue(_uvBuffer))
                    return Fail("video capture replaced a caller-owned NV12 plane pointer", out error);
                if (param.Interlaced != 0)
                    return Fail(Invariant($"interlaced NV12 is unsupported (value={param.Interlaced})"), out error);
                if (param.YSize > YCapacity || param.UvSize > UvCapacity || param.YSize > int.MaxValue || param.UvSize > int.MaxValue)
                    return Fail("video capture returned plane sizes outside the allocated buffers", out error);
                if (param.Width > int.MaxValue || param.Height > int.MaxValue ||
                    param.YPitch > int.MaxValue || param.UvPitch > int.MaxValue)
                    return Fail("video capture returned dimensions or pitches outside supported bounds", out error);

                int width = (int)param.Width;
                int height = (int)param.Height;
                int yPitch = (int)param.YPitch;
                int uvPitch = (int)param.UvPitch;
                int yAvailable = (int)param.YSize;
                int uvAvailable = (int)param.UvSize;
                if (!Nv12FrameConverter.TryValidateLayout(width, height, yPitch, uvPitch,
                    yAvailable, uvAvailable, out int yBytes, out int uvBytes, out error)) return false;

                Marshal.Copy(_yBuffer, _yPlane, 0, yBytes);
                Marshal.Copy(_uvBuffer, _uvPlane, 0, uvBytes);
                if (!Nv12FrameConverter.TryConvert(_yPlane, _uvPlane, width, height, yPitch, uvPitch,
                    yBytes, uvBytes, out byte[] rgb, out error)) return false;

                frame = new PixelFrame(width, height, rgb);
                return true;
            }
            catch (Exception ex)
            {
                return Fail(Invariant($"{CaptureSymbol} failed: {ex.Message}"), out error);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _capture = null;
            if (_yBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_yBuffer); _yBuffer = IntPtr.Zero; }
            if (_uvBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_uvBuffer); _uvBuffer = IntPtr.Zero; }
            if (_library != IntPtr.Zero) { NativeLibrary.Free(_library); _library = IntPtr.Zero; }
        }

        private static bool HasVerifiedLayout(out string error)
        {
            error = null;
            if (Marshal.SizeOf(typeof(CaptureParam)) != ParamSize ||
                OffsetOf(nameof(CaptureParam.YSize)) != 0 ||
                OffsetOf(nameof(CaptureParam.UvSize)) != 4 ||
                OffsetOf(nameof(CaptureParam.Width)) != 8 ||
                OffsetOf(nameof(CaptureParam.Height)) != 0x0c ||
                OffsetOf(nameof(CaptureParam.YPointer)) != 0x10 ||
                OffsetOf(nameof(CaptureParam.UvPointer)) != 0x14 ||
                OffsetOf(nameof(CaptureParam.Interlaced)) != 0x28 ||
                OffsetOf(nameof(CaptureParam.YPitch)) != 0x2c ||
                OffsetOf(nameof(CaptureParam.UvPitch)) != 0x30)
            {
                error = "secvideo_capture_param does not match the verified ARM32 layout";
                return false;
            }
            return true;
        }

        private static int OffsetOf(string field) => Marshal.OffsetOf(typeof(CaptureParam), field).ToInt32();
        private static uint PointerValue(IntPtr pointer) => unchecked((uint)pointer.ToInt32());

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
