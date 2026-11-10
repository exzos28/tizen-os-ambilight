#pragma once
// How UDP frames become LED colors, free of Arduino/FastLED for host tests
// (pio test -e native, see test/test_color_pipeline). LEDController runs it:
//
//   per frame (~7 fps)   samples → ringSmooth (1-2-1 around the screen) → saturate (around
//                        Rec.709 luma) → interpolateSide (RGB, per side) → strip order →
//                        darkThreshold (hysteresis on the output luma) → onFrame (picks the
//                        slow or fast time constant per LED)
//   per update (~60 Hz)  approach the target in 8.8 fixed point with that time constant →
//                        output LUT (white balance, then gamma)
//
// Colors stay RGB throughout: nothing goes through hue, so dark or grey samples, whose hue
// is undefined and noisy, can't invent colors between them.
#include <math.h>
#include <stdint.h>
#include "StripLayout.h"

namespace ColorPipeline {

struct Rgb {
    uint8_t r, g, b;
};

inline bool operator==(Rgb a, Rgb b) { return a.r == b.r && a.g == b.g && a.b == b.b; }
inline bool operator!=(Rgb a, Rgb b) { return !(a == b); }

// ---------------------------------------------------------------------------- constants

// Per-LED dark threshold on the output luma (after white balance and gamma, before the global
// brightness): a lit LED goes black below DARK_OFF_LUMA, a dark one lights again only above
// DARK_ON_LUMA. Output values this low are only a few PWM steps, where a near-black scene
// toggles LEDs on and off and rounding tints them; the gap keeps a value hovering at the
// threshold from flickering.
constexpr uint8_t DARK_OFF_LUMA = 6;
constexpr uint8_t DARK_ON_LUMA  = 10;
static_assert(DARK_OFF_LUMA < DARK_ON_LUMA, "the dark threshold needs a hysteresis gap");

// A change of more than LARGE_CHANGE_LUMA (20 % of full scale, screen-referred luma) between
// what an LED shows and its new target is a large one and uses FAST_TAU_MS instead of the
// smoothing_ms setting, darker or brighter alike. So does every LED when the average luma of
// a frame differs from the previous frame's by more than that (a scene cut).
constexpr uint8_t  LARGE_CHANGE_LUMA = 51;
constexpr uint16_t FAST_TAU_MS       = 40;

// Smoothing factors are Q12: 4096 = jump to the target.
constexpr uint16_t ALPHA_ONE = 4096;

// ---------------------------------------------------------------------------- color

// Rec.709 luma, 0-255 (weights 54 + 183 + 19 = 256).
inline uint8_t luma(uint8_t r, uint8_t g, uint8_t b) {
    return (uint8_t)((54u * r + 183u * g + 19u * b + 128u) >> 8);
}
inline uint8_t luma(Rgb c) { return luma(c.r, c.g, c.b); }

// Saturation factor in 8.8 fixed point (256 = 1.0, unchanged).
inline uint16_t saturationQ8(float s) {
    if (!(s > 0.0f)) return 0;   // also NaN
    float q = s * 256.0f + 0.5f;
    return q >= 65535.0f ? 65535 : (uint16_t)q;
}

// c' = Y + (c − Y)·s per channel, Y = Rec.709 luma, clamped to 0-255. s = 1.0 (256) returns
// the color unchanged; 0 gives the grey of the same luma.
inline Rgb saturate(Rgb c, uint16_t sQ8) {
    if (sQ8 == 256) return c;
    int32_t y = luma(c);
    auto ch = [&](uint8_t v) -> uint8_t {
        int32_t d = ((int32_t)v - y) * (int32_t)sQ8;   // |d| ≤ 255 · 65535
        int32_t delta = d >= 0 ? (d + 128) / 256 : -((-d + 128) / 256);
        int32_t out = y + delta;
        return (uint8_t)(out < 0 ? 0 : out > 255 ? 255 : out);
    };
    return {ch(c.r), ch(c.g), ch(c.b)};
}

// a → b by frac/255 per channel (0 = a, 255 = b exactly).
inline uint8_t lerp8(uint8_t a, uint8_t b, uint8_t frac) {
    return (uint8_t)(((uint32_t)a * (255u - frac) + (uint32_t)b * frac + 127u) / 255u);
}
inline Rgb lerp(Rgb a, Rgb b, uint8_t frac) {
    return {lerp8(a.r, b.r, frac), lerp8(a.g, b.g, frac), lerp8(a.b, b.b, frac)};
}

// ---------------------------------------------------------------------------- frame

// 1-2-1 smoothing of the n samples of the screen ring (top left→right, right top→bottom,
// bottom right→left, left bottom→top — the UDP frame order), wrapping from the last sample to
// the first, so every corner blends with the next side. One noisy sample then moves its part
// of the strip by half, not all the way. in and out must not overlap.
inline void ringSmooth(const Rgb* in, Rgb* out, uint16_t n) {
    if (n == 0) return;
    if (n < 3) {   // no two distinct neighbours: nothing to smooth against
        for (uint16_t i = 0; i < n; i++) out[i] = in[i];
        return;
    }
    for (uint16_t i = 0; i < n; i++) {
        const Rgb& p = in[i == 0 ? n - 1 : i - 1];
        const Rgb& c = in[i];
        const Rgb& q = in[i + 1 == n ? 0 : i + 1];
        out[i] = {(uint8_t)((p.r + 2u * c.r + q.r + 2u) >> 2),
                  (uint8_t)((p.g + 2u * c.g + q.g + 2u) >> 2),
                  (uint8_t)((p.b + 2u * c.b + q.b + 2u) >> 2)};
    }
}

// Average luma of n samples (0 for none).
inline uint8_t averageLuma(const Rgb* s, uint16_t n) {
    if (n == 0) return 0;
    uint32_t sum = 0;
    for (uint16_t i = 0; i < n; i++) sum += luma(s[i]);
    return (uint8_t)((sum + n / 2) / n);
}

// Spreads one side's srcCount samples over its dstCount LEDs, linear in RGB between
// neighbouring samples (positions: StripLayout::interpPosition). No samples: the side is
// dark. Out: any type with r, g, b members (CRGB on the device).
template <typename Out>
inline void interpolateSide(const Rgb* src, uint8_t srcCount, Out* dst, uint8_t dstCount) {
    for (uint8_t i = 0; i < dstCount; i++) {
        Rgb c{0, 0, 0};
        if (srcCount > 0) {
            StripLayout::InterpPos p = StripLayout::interpPosition(i, dstCount, srcCount);
            c = p.lo == p.hi ? src[p.lo] : lerp(src[p.lo], src[p.hi], p.frac);
        }
        dst[i].r = c.r;
        dst[i].g = c.g;
        dst[i].b = c.b;
    }
}

// ---------------------------------------------------------------------------- output

// Gamma table: table[v] = round(255 · (v/255)^gamma).
inline void buildGammaTable(float gamma, uint8_t table[256]) {
    for (int v = 0; v < 256; v++)
        table[v] = (uint8_t)(pow((double)v / 255.0, (double)gamma) * 255.0 + 0.5);
}

// White balance then gamma, one lookup table per channel: r[v] = gamma[v · wbR / 255].
struct OutputLut {
    uint8_t r[256], g[256], b[256];
    Rgb apply(Rgb c) const { return {r[c.r], g[c.g], b[c.b]}; }
};

inline void buildOutputLut(const uint8_t gammaTable[256], uint8_t wbR, uint8_t wbG, uint8_t wbB,
                           OutputLut& lut) {
    for (int v = 0; v < 256; v++) {
        lut.r[v] = gammaTable[wbR == 255 ? v : v * wbR / 255];
        lut.g[v] = gammaTable[wbG == 255 ? v : v * wbG / 255];
        lut.b[v] = gammaTable[wbB == 255 ? v : v * wbB / 255];
    }
}

// ---------------------------------------------------------------------------- per LED

// What the smoothing keeps per LED: the shown color (screen-referred, before white balance
// and gamma) in 8.8 fixed point, and two flags.
struct LedState {
    uint16_t c[3];   // r, g, b × 256
    uint8_t  flags;  // LED_LIT, LED_FAST
};
constexpr uint8_t LED_LIT  = 1;   // dark threshold: currently lit
constexpr uint8_t LED_FAST = 2;   // the transition to the current target uses FAST_TAU_MS

inline uint8_t shownChannel(uint16_t v) { return (uint8_t)((v + 128u) >> 8); }
inline Rgb shown(const LedState& s) {
    return {shownChannel(s.c[0]), shownChannel(s.c[1]), shownChannel(s.c[2])};
}

// Back to black, unlit, nothing pending (e.g. after the strip was drawn black elsewhere).
inline void clear(LedState& s) {
    s.c[0] = s.c[1] = s.c[2] = 0;
    s.flags = 0;
}

// Dark threshold with hysteresis: whether an LED is lit with this output luma, given whether
// it was.
inline bool nextLit(bool lit, uint8_t outLuma) {
    return lit ? outLuma >= DARK_OFF_LUMA : outLuma > DARK_ON_LUMA;
}

// Applies the dark threshold to an LED's new target (outLuma: its luma after the output LUT):
// updates LED_LIT and blackens the target while unlit.
inline void darkThreshold(LedState& s, Rgb& target, uint8_t outLuma) {
    bool lit = nextLit((s.flags & LED_LIT) != 0, outLuma);
    s.flags = (uint8_t)(lit ? (s.flags | LED_LIT) : (s.flags & ~LED_LIT));
    if (!lit) target = {0, 0, 0};
}

inline bool largeChange(uint8_t fromLuma, uint8_t toLuma) {
    int d = (int)fromLuma - (int)toLuma;
    return (d < 0 ? -d : d) > LARGE_CHANGE_LUMA;
}

inline bool sceneCut(uint8_t prevAverageLuma, uint8_t averageLuma) {
    return largeChange(prevAverageLuma, averageLuma);
}

// A new target arrived: the transition to it is fast on a scene cut or when it differs from
// what the LED shows by a large luma step (either direction), slow otherwise — so a change of
// hue alone, like sample noise, stays smoothed.
inline void onFrame(LedState& s, Rgb target, bool cut) {
    bool fast = cut || largeChange(luma(shown(s)), luma(target));
    s.flags = (uint8_t)(fast ? (s.flags | LED_FAST) : (s.flags & ~LED_FAST));
}

// Smoothing factor for dtMs elapsed with time constant tauMs: 1 − e^(−dt/τ) in Q12, at least
// 1 so it always moves; tau 0 = jump. Real time, so the result doesn't depend on how often
// update() runs.
inline uint16_t alphaQ12(uint32_t dtMs, uint16_t tauMs) {
    if (tauMs == 0) return ALPHA_ONE;
    float a = 1.0f - expf(-(float)dtMs / (float)tauMs);
    uint32_t q = (uint32_t)(a * ALPHA_ONE + 0.5f);
    return (uint16_t)(q < 1 ? 1 : q > ALPHA_ONE ? ALPHA_ONE : q);
}

// One channel one step toward target (8-bit) by alpha (Q12). Never overshoots; a step that
// would round to nothing lands on the target (then < 2048/alpha units of 1/256 away: about
// 1/8 of a level at the slowest factor update() uses, 16 ms with a 1000 ms time constant).
inline uint16_t approach(uint16_t v, uint8_t target, uint16_t alpha) {
    int32_t t = (int32_t)target << 8;
    int32_t d = t - (int32_t)v;   // |d| ≤ 65280, · 4096 fits
    if (d == 0) return v;
    int32_t step = d > 0 ? (d * alpha + 2048) >> 12 : -(((-d) * alpha + 2048) >> 12);
    return (uint16_t)(step == 0 ? t : (int32_t)v + step);
}

// Advances one LED toward its target with the slow or fast factor its LED_FAST flag picks.
inline void step(LedState& s, Rgb target, uint16_t slowAlpha, uint16_t fastAlpha) {
    uint16_t a = (s.flags & LED_FAST) ? fastAlpha : slowAlpha;
    s.c[0] = approach(s.c[0], target.r, a);
    s.c[1] = approach(s.c[1], target.g, a);
    s.c[2] = approach(s.c[2], target.b, a);
}

// The two factors for one update: slow from the smoothing_ms setting, fast from FAST_TAU_MS
// (never slower than the slow one, for settings below it).
struct Alphas {
    uint16_t slow, fast;
};
inline Alphas alphas(uint32_t dtMs, uint16_t smoothingMs) {
    uint16_t slow = alphaQ12(dtMs, smoothingMs);
    uint16_t fast = alphaQ12(dtMs, FAST_TAU_MS);
    return {slow, fast > slow ? fast : slow};
}

}  // namespace ColorPipeline
