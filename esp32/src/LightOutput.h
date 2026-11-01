#pragma once
// What the strip shows, when UDP frames are applied, and the identify blink timeline. Free
// of Arduino/FastLED for host tests (pio test -e native, see test/test_light_output).
#include <stdint.h>
#include "generated/ProtocolLimits.h"

namespace LightOutput {

// What currently owns the strip, highest priority first.
enum class Source : uint8_t {
    Identify,   // POST /identify blink; runs even with the lighting off
    Preview,    // calibration preview; runs even with the lighting off
    Dark,       // lighting off (power = false): dark in both modes
    Plain,      // ledMode LED_MODE_PLAIN: static color
    Dynamic,    // ledMode LED_MODE_DYNAMIC: UDP frames
};

inline Source source(bool power, uint8_t ledMode, bool identifying, bool previewing) {
    if (identifying) return Source::Identify;
    if (previewing)  return Source::Preview;
    if (!power)      return Source::Dark;
    return ledMode == protocol::LED_MODE_PLAIN ? Source::Plain : Source::Dynamic;
}

// Whether a valid UDP frame is applied (decoded into the target the strip blends toward).
// An identify blink does not block frames: the target stays current, and the strip blends
// back to it when the blink ends. While this is false the target is stale: repaint()
// clears it, so turning the lighting (or dynamic mode) back on stays dark until a fresh
// frame arrives instead of fading in an old one.
inline bool acceptsFrames(bool power, uint8_t ledMode, bool previewing) {
    return power && ledMode == protocol::LED_MODE_DYNAMIC && !previewing;
}

// Whether a redraw of the regular output (repaint(), a new LED count, brightness or power
// limit) may draw on the strip now. During an identify blink it only updates state: the
// blink redraws its current phase with the new state on the next update() and repaint()s
// when it ends.
inline bool redrawsNow(Source current) {
    return current != Source::Identify;
}

// POST /identify: BLINKS times white, then dark, over exactly DURATION_MS, then the
// previous output. The duration is split into 2 × BLINKS equal slots (on, off, on, …), so
// the blink ends after a dark slot and the restored output is visibly separate.
namespace Identify {

constexpr uint8_t  BLINKS      = protocol::ESP32_IDENTIFY_BLINKS;
constexpr uint32_t DURATION_MS = protocol::ESP32_IDENTIFY_DURATION_MS;
static_assert(BLINKS > 0, "identify needs at least one blink");
static_assert(DURATION_MS >= 2u * BLINKS, "identify slots must be at least 1 ms");

enum class Phase : uint8_t { On, Off, Done };

inline Phase phase(uint32_t elapsedMs) {
    if (elapsedMs >= DURATION_MS) return Phase::Done;
    uint32_t slot = (uint32_t)((uint64_t)elapsedMs * (2u * BLINKS) / DURATION_MS);
    return slot % 2 == 0 ? Phase::On : Phase::Off;
}

// LEDs a blink frame covers: every LED that may be lit — the driven count, LEDs a
// calibration preview lit beyond it (previewLit) and the calibrated layout (sum of the
// sides) — capped at maxLeds.
inline uint16_t coverage(uint16_t numLeds, uint16_t previewLit, uint16_t layoutTotal,
                         uint16_t maxLeds) {
    uint16_t n = numLeds;
    if (previewLit  > n) n = previewLit;
    if (layoutTotal > n) n = layoutTotal;
    return n < maxLeds ? n : maxLeds;
}

// What update() does at elapsedMs into a blink. `drawn`: the phase last drawn by the blink,
// Phase::Done if nothing is drawn yet or another redraw request (LED count, brightness…)
// arrived since, which the blink re-applies its phase over.
enum class Step : uint8_t {
    Keep,     // the strip already shows the current phase
    Draw,     // draw phase(elapsedMs) over coverage()
    Finish,   // blink over: clear what it covered, then repaint()
};

inline Step step(uint32_t elapsedMs, Phase drawn) {
    Phase p = phase(elapsedMs);
    if (p == Phase::Done) return Step::Finish;
    return p == drawn ? Step::Keep : Step::Draw;
}

// LEDs the show of the repaint() after a blink pushes: what the repaint asks for
// (`requested`, possibly fewer than the blink lit, or none), but at least `cleared` — the
// LEDs the blink covered, cleared in the buffer beforehand — so that one show both draws the
// restored output and darkens the rest; capped at maxLeds.
inline uint16_t pushCount(uint16_t requested, uint16_t cleared, uint16_t maxLeds) {
    uint16_t n = requested > cleared ? requested : cleared;
    return n < maxLeds ? n : maxLeds;
}

}  // namespace Identify

}  // namespace LightOutput
