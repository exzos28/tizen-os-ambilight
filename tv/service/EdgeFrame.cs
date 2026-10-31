using System;

namespace Service
{
    // One captured frame of screen-edge colors.
    //
    // Rgb holds 2 × (H + V) samples, 3 bytes each, clockwise from the top-left corner:
    //   top    H samples, left → right
    //   right  V samples, top → bottom
    //   bottom H samples, right → left
    //   left   V samples, bottom → top
    // This is also the payload layout of the ESP32 UDP frame (docs/NETWORKING.md).
    sealed class EdgeFrame
    {
        public int H { get; }
        public int V { get; }
        public byte[] Rgb { get; }

        public EdgeFrame(int h, int v, byte[] rgb)
        {
            if (h < 1 || v < 1) throw new ArgumentOutOfRangeException(nameof(h), "need at least one sample per edge");
            if (rgb == null || rgb.Length != 2 * (h + v) * 3)
                throw new ArgumentException($"expected {2 * (h + v) * 3} bytes, got {rgb?.Length ?? 0}", nameof(rgb));
            H = h;
            V = v;
            Rgb = rgb;
        }

        public int TopOffset    => 0;
        public int RightOffset  => H * 3;
        public int BottomOffset => (H + V) * 3;
        public int LeftOffset   => (2 * H + V) * 3;
    }
}
