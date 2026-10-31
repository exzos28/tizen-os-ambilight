#pragma once
// Pure strip-layout math, free of Arduino/FastLED so it can be unit-tested on the host
// (pio test -e native, see test/test_strip_layout).
#include <stdint.h>

namespace StripLayout {

// Sides in clockwise order. Each side's natural direction (index 0 first) starts at the
// corner where a clockwise strip would enter it:
//   TOP[0] = top-left, RIGHT[0] = top-right, BOTTOM[0] = bottom-right, LEFT[0] = bottom-left.
enum Side : uint8_t { Top = 0, Right = 1, Bottom = 2, Left = 3 };

// startCorner: 0 = top-left, 1 = top-right, 2 = bottom-right, 3 = bottom-left.

// Side covered by the s-th segment (0..3) of the strip.
//   Clockwise from corner c:         sides c, c+1, c+2, c+3 (mod 4), each in natural order.
//   Counter-clockwise from corner c: sides c+3, c+2, c+1, c   (mod 4), each reversed.
inline uint8_t sideAt(uint8_t startCorner, bool clockwise, uint8_t s) {
    return clockwise ? (uint8_t)((startCorner + s) % 4)
                     : (uint8_t)((startCorner + 3 - s) % 4);
}

// Walks the strip from its first LED: calls fn(stripIndex, side, indexWithinSide) for each
// LED, stopping after maxLeds. counts[side] = LEDs on that side. Returns LEDs visited.
template <typename Fn>
inline uint16_t forEach(uint8_t startCorner, bool clockwise, const uint8_t counts[4],
                        uint16_t maxLeds, Fn fn) {
    uint16_t idx = 0;
    for (uint8_t s = 0; s < 4; s++) {
        uint8_t side = sideAt(startCorner, clockwise, s);
        uint8_t n = counts[side];
        for (uint16_t k = 0; k < n; k++) {
            if (idx >= maxLeds) return idx;
            uint8_t i = clockwise ? (uint8_t)k : (uint8_t)(n - 1 - k);
            fn(idx++, side, i);
        }
    }
    return idx;
}

// Where LED i of a side with dstCount LEDs falls between that edge's srcCount samples:
// blend sample lo into hi by frac/255. The first and last LED sit on the first and last
// sample (the corners); a single LED takes the middle of the edge.
struct InterpPos {
    uint8_t lo;
    uint8_t hi;
    uint8_t frac;
};

inline InterpPos interpPosition(uint8_t i, uint8_t dstCount, uint8_t srcCount) {
    if (srcCount <= 1) return {0, 0, 0};
    float t = dstCount <= 1 ? (srcCount - 1) * 0.5f
                            : (float)i / (dstCount - 1) * (srcCount - 1);
    uint8_t lo = (uint8_t)t;
    if (lo > srcCount - 1) lo = srcCount - 1;
    uint8_t hi = (uint8_t)(lo + 1 < srcCount ? lo + 1 : lo);
    return {lo, hi, (uint8_t)((t - lo) * 255.0f)};
}

}  // namespace StripLayout
