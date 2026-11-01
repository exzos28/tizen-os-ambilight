#pragma once
// What the strip shows, when UDP frames are applied, and the identify blink timeline. Free
// of Arduino/FastLED for host tests (pio test -e native, see test/test_light_output).
#include <stdint.h>

namespace LightOutput {

// What currently owns the strip, highest priority first.
enum class Source : uint8_t {
    Identify,   // POST /identify blink; runs even with the lighting off
    Preview,    // calibration preview; runs even with the lighting off
    Dark,       // lighting off (power = false): dark in both modes
    Plain,      // ledMode 0: static color
    Dynamic,    // ledMode 1: UDP frames
};

inline Source source(bool power, uint8_t ledMode, bool identifying, bool previewing) {
    if (identifying) return Source::Identify;
    if (previewing)  return Source::Preview;
    if (!power)      return Source::Dark;
    return ledMode == 0 ? Source::Plain : Source::Dynamic;
}

// Whether a valid UDP frame is applied (decoded into the target the strip blends toward).
// An identify blink does not block frames: the target stays current, and the strip blends
// back to it when the blink ends.
inline bool acceptsFrames(bool power, uint8_t ledMode, bool previewing) {
    return power && ledMode == 1 && !previewing;
}

// POST /identify: BLINKS times white for ON_MS, dark for OFF_MS, then the previous output.
namespace Identify {

constexpr uint8_t  BLINKS      = 3;
constexpr uint32_t ON_MS       = 333;
constexpr uint32_t OFF_MS      = 333;
constexpr uint32_t PERIOD_MS   = ON_MS + OFF_MS;
constexpr uint32_t DURATION_MS = BLINKS * PERIOD_MS;   // 1998 ms

enum class Phase : uint8_t { On, Off, Done };

inline Phase phase(uint32_t elapsedMs) {
    if (elapsedMs >= DURATION_MS) return Phase::Done;
    return elapsedMs % PERIOD_MS < ON_MS ? Phase::On : Phase::Off;
}

}  // namespace Identify

}  // namespace LightOutput
