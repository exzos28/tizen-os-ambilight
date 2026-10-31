#pragma once
// Strip power estimate and limit, free of Arduino/FastLED for host tests
// (pio test -e native, see test/test_power_limit).
#include <stdint.h>

namespace PowerLimit {

struct Result {
    uint8_t brightness;   // global brightness to use for this frame
    float   watts;        // estimated power at that brightness
    bool    limited;      // brightness was reduced to stay within the limit
};

// channelSum: sum of R+G+B over all driven LEDs (0..765 per LED). Channel values are PWM
// duty cycles, so power is roughly linear in them: a pixel at full white and brightness
// 255 draws pixelWatts. limitW = 0 means no limit.
inline Result apply(uint32_t channelSum, uint8_t brightness, float pixelWatts, uint16_t limitW) {
    float watts = channelSum / 765.0f * pixelWatts * (brightness / 255.0f);
    if (limitW == 0 || watts <= limitW) return {brightness, watts, false};
    uint8_t scaled = (uint8_t)(brightness * (limitW / watts));   // rounds down: stays within the limit
    return {scaled, channelSum / 765.0f * pixelWatts * (scaled / 255.0f), true};
}

}  // namespace PowerLimit
