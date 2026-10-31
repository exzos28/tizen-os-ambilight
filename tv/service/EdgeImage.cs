using System;

namespace Service
{
    // Turns an EdgeFrame into a small RGB image for image-based receivers (HyperHDR).
    //
    // HyperHDR assigns each LED a rectangle of the input image (its own LED layout) and
    // averages the pixels inside; it does no interpolation of its own. So every pixel here
    // takes its color from the nearest screen edge, linearly interpolated along that edge
    // between the neighbouring samples. The first and last sample of an edge map to its
    // corners, like LEDController::interpolateSide on the ESP32, so both outputs look alike.
    // Which edge "owns" a pixel is decided by pixel distance, so the result does not depend
    // on the LED area depth configured in HyperHDR.
    static class EdgeImage
    {
        public const int MinSize = 8;
        public const int MaxSize = 256;

        // Renders into a new width × height × 3 byte array (row-major, RGB).
        public static byte[] Render(EdgeFrame frame, int width, int height)
        {
            if (width < MinSize || width > MaxSize) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < MinSize || height > MaxSize) throw new ArgumentOutOfRangeException(nameof(height));

            var img = new byte[width * height * 3];
            var rgb = frame.Rgb;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Distance from the pixel centre to each edge, in pixels.
                    double dTop = y + 0.5, dBottom = height - y - 0.5;
                    double dLeft = x + 0.5, dRight = width - x - 0.5;
                    double min = Math.Min(Math.Min(dTop, dBottom), Math.Min(dLeft, dRight));

                    int baseOffset, count;
                    double s;   // position along the edge in sample order, 0..1
                    if (min == dTop)        { baseOffset = frame.TopOffset;    count = frame.H; s = (double)x / (width - 1); }
                    else if (min == dRight) { baseOffset = frame.RightOffset;  count = frame.V; s = (double)y / (height - 1); }
                    else if (min == dBottom){ baseOffset = frame.BottomOffset; count = frame.H; s = (double)(width - 1 - x) / (width - 1); }
                    else                    { baseOffset = frame.LeftOffset;   count = frame.V; s = (double)(height - 1 - y) / (height - 1); }

                    int o = (y * width + x) * 3;
                    Sample(rgb, baseOffset, count, s, img, o);
                }
            }
            return img;
        }

        // Linear interpolation between neighbouring samples of one edge.
        private static void Sample(byte[] rgb, int baseOffset, int count, double s, byte[] dst, int o)
        {
            if (count == 1)
            {
                dst[o] = rgb[baseOffset]; dst[o + 1] = rgb[baseOffset + 1]; dst[o + 2] = rgb[baseOffset + 2];
                return;
            }
            double t = s * (count - 1);
            int lo = Math.Min((int)t, count - 1);
            int hi = Math.Min(lo + 1, count - 1);
            double f = t - lo;
            int a = baseOffset + lo * 3, b = baseOffset + hi * 3;
            for (int c = 0; c < 3; c++)
                dst[o + c] = (byte)Math.Round(rgb[a + c] + (rgb[b + c] - rgb[a + c]) * f);
        }
    }
}
