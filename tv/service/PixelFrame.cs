using System;

namespace Service
{
    // A bounded, immutable-by-convention RGB image of the full video plane.
    sealed class PixelFrame
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Rgb { get; }

        public PixelFrame(int width, int height, byte[] rgb)
        {
            if (width < 1 || width > Nv12FrameConverter.MaxWidth) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1 || height > Nv12FrameConverter.MaxHeight) throw new ArgumentOutOfRangeException(nameof(height));
            if (rgb == null || rgb.Length != width * height * 3)
                throw new ArgumentException("rgb must be width*height*3 bytes", nameof(rgb));
            Width = width;
            Height = height;
            Rgb = rgb;
        }
    }

    // One pending HyperHDR input: either a real full-frame image or the existing edge fallback.
    sealed class HyperHdrFrame
    {
        public EdgeFrame Edge { get; }
        public PixelFrame Pixels { get; }

        private HyperHdrFrame(EdgeFrame edge, PixelFrame pixels)
        {
            Edge = edge;
            Pixels = pixels;
        }

        public static HyperHdrFrame FromEdge(EdgeFrame frame) =>
            new HyperHdrFrame(frame ?? throw new ArgumentNullException(nameof(frame)), null);

        public static HyperHdrFrame FromPixels(PixelFrame frame) =>
            new HyperHdrFrame(null, frame ?? throw new ArgumentNullException(nameof(frame)));
    }

    // Pure NV12 layout validation and conversion. NV12 stores one 8-bit Y byte per pixel,
    // followed by interleaved U/V bytes for each 2x2 luma block; row pitches are in bytes.
    static class Nv12FrameConverter
    {
        public const int MaxWidth = 960;
        public const int MaxHeight = 540;
        public const int MaxOutputWidth = 160;
        public const int MaxOutputHeight = 90;

        public static bool TryConvert(byte[] yPlane, byte[] uvPlane, int width, int height,
            int yPitch, int uvPitch, out byte[] rgb, out string error) =>
            TryConvert(yPlane, uvPlane, width, height, yPitch, uvPitch,
                yPlane?.Length ?? 0, uvPlane?.Length ?? 0, out rgb, out error);

        internal static bool TryConvert(byte[] yPlane, byte[] uvPlane, int width, int height,
            int yPitch, int uvPitch, int yAvailable, int uvAvailable, out byte[] rgb, out string error)
        {
            return TryConvert(yPlane, uvPlane, width, height, yPitch, uvPitch, yAvailable, uvAvailable,
                out rgb, out _, out _, out error);
        }

        internal static bool TryConvert(byte[] yPlane, byte[] uvPlane, int width, int height,
            int yPitch, int uvPitch, int yAvailable, int uvAvailable,
            out byte[] rgb, out int outputWidth, out int outputHeight, out string error)
        {
            rgb = null;
            outputWidth = outputHeight = 0;
            error = null;
            if (yPlane == null || uvPlane == null) return Fail("NV12 planes are missing", out error);
            if (yAvailable > yPlane.Length || uvAvailable > uvPlane.Length || yAvailable < 0 || uvAvailable < 0)
                return Fail("NV12 plane sizes exceed the supplied buffers", out error);
            if (!TryValidateLayout(width, height, yPitch, uvPitch, yAvailable, uvAvailable,
                out _, out _, out error)) return false;

            int scale = Math.Max(1, Math.Max((width + MaxOutputWidth - 1) / MaxOutputWidth,
                (height + MaxOutputHeight - 1) / MaxOutputHeight));
            outputWidth = (width + scale - 1) / scale;
            outputHeight = (height + scale - 1) / scale;
            rgb = new byte[outputWidth * outputHeight * 3];
            for (int y = 0; y < outputHeight; y++)
            {
                int yStart = y * scale, yEnd = Math.Min(yStart + scale, height);
                for (int x = 0; x < outputWidth; x++)
                {
                    int xStart = x * scale, xEnd = Math.Min(xStart + scale, width);
                    int sumY = 0, sumU = 0, sumV = 0, count = 0;
                    for (int sourceY = yStart; sourceY < yEnd; sourceY++)
                    {
                        int yRow = sourceY * yPitch, uvRow = (sourceY >> 1) * uvPitch;
                        for (int sourceX = xStart; sourceX < xEnd; sourceX++)
                        {
                            int uv = uvRow + (sourceX & ~1);
                            sumY += yPlane[yRow + sourceX];
                            sumU += uvPlane[uv];
                            sumV += uvPlane[uv + 1];
                            count++;
                        }
                    }
                    int luminance = Math.Max(0, sumY / count - 16);
                    int u = sumU / count - 128;
                    int v = sumV / count - 128;
                    int o = (y * outputWidth + x) * 3;
                    rgb[o] = Clamp((298 * luminance + 409 * v + 128) >> 8);
                    rgb[o + 1] = Clamp((298 * luminance - 100 * u - 208 * v + 128) >> 8);
                    rgb[o + 2] = Clamp((298 * luminance + 516 * u + 128) >> 8);
                }
            }
            return true;
        }

        internal static bool TryValidateLayout(int width, int height, int yPitch, int uvPitch,
            int yAvailable, int uvAvailable, out int yBytes, out int uvBytes, out string error)
        {
            yBytes = 0;
            uvBytes = 0;
            error = null;
            if (width < 2 || width > MaxWidth || height < 2 || height > MaxHeight ||
                (width & 1) != 0 || (height & 1) != 0)
                return Fail($"unsupported NV12 dimensions {width}x{height}", out error);
            if (yPitch < width || yPitch > MaxWidth || uvPitch < width || uvPitch > MaxWidth)
                return Fail($"invalid NV12 pitches y={yPitch} uv={uvPitch} for width={width}", out error);

            long requiredY = (long)yPitch * height;
            long requiredUv = (long)uvPitch * (height / 2);
            if (requiredY > int.MaxValue || requiredUv > int.MaxValue ||
                yAvailable < requiredY || uvAvailable < requiredUv)
                return Fail($"incomplete NV12 planes y={yAvailable}/{requiredY} uv={uvAvailable}/{requiredUv}", out error);

            yBytes = (int)requiredY;
            uvBytes = (int)requiredUv;
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
    }
}
