#pragma once
// How UDP frames become LED colors, free of Arduino/FastLED for host tests
// (pio test -e native, see test/test_color_pipeline). LEDController runs it:
//
//   per frame (~7 fps)   samples → ringSmooth (1-2-1 around the screen) → saturate (around
//                        Rec.709 luma) → interpolateSide (RGB, per side) → strip order →
//                        onFrame (picks the path per LED from the change since the last frame)
//                        → darkThreshold (hysteresis on the output luma)
//   per update (~60 Hz)  approach the target in 8.8 fixed point with that path's time constant →
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
constexpr uint8_t DARK_OFF_LUMA = 2;
constexpr uint8_t DARK_ON_LUMA  = 4;
static_assert(DARK_OFF_LUMA < DARK_ON_LUMA, "the dark threshold needs a hysteresis gap");

// How an LED follows a new target (Path), from how its target changed since the last frame:
// - a scene cut (the frame's average luma moves by more than LARGE_CHANGE_LUMA, 20 % of full
//   scale) and a large step down: FAST_TAU_MS, so cuts and cuts to dark stay crisp;
// - a large step up (luma by more than LARGE_CHANGE_LUMA, or a channel by more than
//   LARGE_CHANGE_CHANNEL: a color cut at the same brightness) without a scene cut: half the
//   smoothing_ms setting, at least FAST_TAU_MS. A flash, a subtitle or a credit line crossing
//   one sample is a single LED-sized step up: it is followed, but softened, not strobed;
// - anything else, including a fade (small steps every frame): the smoothing_ms setting;
// - the first frame after the strip was dark or cleared, and going dark when the frames stop:
//   FADE_TAU_MS, a gentle fade instead of a pop.
// Comparing with the last frame's target (not with what the LED shows) keeps a slow fade on
// the slow path even when the shown color lags behind it.
constexpr uint8_t  LARGE_CHANGE_LUMA    = 51;
constexpr uint8_t  LARGE_CHANGE_CHANNEL = 96;
constexpr uint16_t FAST_TAU_MS          = 40;
constexpr uint16_t FADE_TAU_MS          = 500;

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

// The time constant an LED follows its target with (see FAST_TAU_MS above).
enum class Path : uint8_t { Slow = 0, Rise = 1, Fast = 2, Fade = 3 };

// What the smoothing keeps per LED: the shown color (screen-referred, before white balance
// and gamma) in 8.8 fixed point, the last frame's target before the dark threshold, and flags.
struct LedState {
    uint16_t c[3];   // r, g, b × 256
    Rgb      prev;   // last frame's target, before the dark threshold
    uint8_t  flags;  // LED_LIT, the Path in PATH_MASK
};
constexpr uint8_t LED_LIT    = 1;   // dark threshold: currently lit
constexpr uint8_t PATH_SHIFT = 1;
constexpr uint8_t PATH_MASK  = 3 << PATH_SHIFT;

inline Path path(const LedState& s) { return (Path)((s.flags & PATH_MASK) >> PATH_SHIFT); }
inline void setPath(LedState& s, Path p) {
    s.flags = (uint8_t)((s.flags & ~PATH_MASK) | ((uint8_t)p << PATH_SHIFT));
}

inline uint8_t shownChannel(uint16_t v) { return (uint8_t)((v + 128u) >> 8); }
inline Rgb shown(const LedState& s) {
    return {shownChannel(s.c[0]), shownChannel(s.c[1]), shownChannel(s.c[2])};
}

// Back to black, unlit, slow path (e.g. after the strip was drawn black elsewhere).
inline void clear(LedState& s) {
    s.c[0] = s.c[1] = s.c[2] = 0;
    s.prev = {0, 0, 0};
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

inline int absDiff(int a, int b) { return a > b ? a - b : b - a; }

inline bool largeLumaChange(uint8_t fromLuma, uint8_t toLuma) {
    return absDiff(fromLuma, toLuma) > LARGE_CHANGE_LUMA;
}

// A large change between two frames' targets: the brightness, or one channel by a lot (a cut
// between two colors of the same brightness). Hue noise in dark or grey samples moves the
// channels by far less.
inline bool largeChange(Rgb from, Rgb to) {
    return largeLumaChange(luma(from), luma(to)) || absDiff(from.r, to.r) > LARGE_CHANGE_CHANNEL
        || absDiff(from.g, to.g) > LARGE_CHANGE_CHANNEL || absDiff(from.b, to.b) > LARGE_CHANGE_CHANNEL;
}

inline bool sceneCut(uint8_t prevAverageLuma, uint8_t averageLuma) {
    return largeLumaChange(prevAverageLuma, averageLuma);
}

// A new frame's target for this LED (before the dark threshold): picks its path from the
// change since the last frame's target. fadeIn: the strip was dark or cleared before this
// frame.
inline void onFrame(LedState& s, Rgb target, bool cut, bool fadeIn) {
    Path p = fadeIn ? Path::Fade
        : cut ? Path::Fast
        : !largeChange(s.prev, target) ? Path::Slow
        : luma(target) < luma(s.prev) ? Path::Fast
        : Path::Rise;
    setPath(s, p);
    s.prev = target;
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

// The factors for one update, one per Path: slow from the smoothing_ms setting; rise from half
// of it, at least FAST_TAU_MS; fast from FAST_TAU_MS; rise and fast never slower than slow
// (for settings below them). Fade from FADE_TAU_MS, never faster than slow.
struct Alphas {
    uint16_t slow, rise, fast, fade;
};
inline Alphas alphas(uint32_t dtMs, uint16_t smoothingMs) {
    uint16_t slow = alphaQ12(dtMs, smoothingMs);
    uint16_t half = (uint16_t)(smoothingMs / 2);
    uint16_t rise = alphaQ12(dtMs, half > FAST_TAU_MS ? half : FAST_TAU_MS);
    uint16_t fast = alphaQ12(dtMs, FAST_TAU_MS);
    uint16_t fade = alphaQ12(dtMs, FADE_TAU_MS);
    return {slow, rise > slow ? rise : slow, fast > slow ? fast : slow, fade < slow ? fade : slow};
}

// Advances one LED toward its target with the factor of its path.
inline void step(LedState& s, Rgb target, const Alphas& a) {
    Path p = path(s);
    uint16_t k = p == Path::Fast ? a.fast : p == Path::Rise ? a.rise : p == Path::Fade ? a.fade : a.slow;
    s.c[0] = approach(s.c[0], target.r, k);
    s.c[1] = approach(s.c[1], target.g, k);
    s.c[2] = approach(s.c[2], target.b, k);
}

}  // namespace ColorPipeline
