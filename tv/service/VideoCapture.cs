using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static System.FormattableString;

namespace Service
{
    interface IVideoCaptureSource : IDisposable
    {
        bool TryCapture(out PixelFrame frame, out string error);
    }

    // The undocumented ABI must be explicitly trusted for this exact library build.
    sealed class VideoCapture : IVideoCaptureSource
    {
        private const string LibraryName = "/usr/lib/libvideo-capture.so.0.1.0";
        private const string CaptureSymbol = "secvideo_api_capture_video_main";
        // Reserve a generous zeroed tail because newer struct fields after the verified pitches
        // (including a timestamp) may be written by the native API.
        private const int ParamSize = 0x100;
        private const int YCapacity = Nv12FrameConverter.MaxWidth * Nv12FrameConverter.MaxHeight;
        private const int UvCapacity = YCapacity / 2;

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
                Clear(_yBuffer, YCapacity);
                Clear(_uvBuffer, UvCapacity);
            }
            catch
            {
                if (_yBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_yBuffer);
                if (_uvBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_uvBuffer);
                _yBuffer = _uvBuffer = IntPtr.Zero;
                throw;
            }
        }

        public static VideoCapture TryOpen(string trustedLibrarySha256, out string error)
        {
            error = null;
            if (IntPtr.Size != 4 || RuntimeInformation.ProcessArchitecture != Architecture.Arm)
            {
                error = $"{CaptureSymbol} requires a 32-bit ARM process";
                return null;
            }
            if (!VerifyLibrary(LibraryName, trustedLibrarySha256, out error)) return null;

            IntPtr library = NativeLibraryHelper.Load(LibraryName, out string loadError);
            if (library == IntPtr.Zero)
            {
                error = $"loading {LibraryName} failed: {loadError}";
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
                Clear(_yBuffer, YCapacity);
                Clear(_uvBuffer, UvCapacity);

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
                if (!HasFrameData(_yPlane, yBytes, _uvPlane, uvBytes))
                    return Fail("video capture returned empty planes", out error);
                if (!Nv12FrameConverter.TryConvert(_yPlane, _uvPlane, width, height, yPitch, uvPitch,
                    yBytes, uvBytes, out byte[] rgb, out int outputWidth, out int outputHeight, out error)) return false;

                frame = new PixelFrame(outputWidth, outputHeight, rgb);
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

        internal static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            return true;
        }

        internal static bool HasFrameData(byte[] yPlane, int yBytes, byte[] uvPlane, int uvBytes)
        {
            for (int i = 0; i < yBytes; i++) if (yPlane[i] != 0) return true;
            for (int i = 0; i < uvBytes; i++) if (uvPlane[i] != 0) return true;
            return false;
        }

        internal static bool VerifyLibrary(string path, string trustedSha256, out string error)
        {
            if (!IsSha256(trustedSha256))
                return Fail("no trusted video-capture library SHA-256 configured; using PPI", out error);
            try
            {
                using var hash = SHA256.Create();
                using var file = File.OpenRead(path);
                string actual = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "");
                if (!string.Equals(actual, trustedSha256, StringComparison.OrdinalIgnoreCase))
                    return Fail("video-capture library SHA-256 does not match the trusted build; using PPI", out error);
            }
            catch (Exception ex) { return Fail($"cannot verify video-capture library: {ex.Message}", out error); }
            error = null;
            return true;
        }

        private static unsafe void Clear(IntPtr buffer, int size) => new Span<byte>(buffer.ToPointer(), size).Clear();
        private static uint PointerValue(IntPtr pointer) => unchecked((uint)pointer.ToInt32());

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
