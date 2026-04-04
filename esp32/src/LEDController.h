#pragma once
#include <Arduino.h>
#include <FastLED.h>

constexpr uint8_t  LED_PIN  = 23;
constexpr uint16_t MAX_LEDS = 1500;

class LEDController {
public:
    static LEDController& instance();

    void begin(uint16_t numLeds, uint8_t brightness = 100);
    void setNumLeds(uint16_t numLeds);
    void setBrightness(uint8_t brightness);
    void setColor(uint8_t r, uint8_t g, uint8_t b);
    void setColors(const CRGB* colors, uint16_t count);

    // Apply one ambilight frame from UDP data.
    // data layout: [hCount top RGB] [vCount right RGB] [hCount bottom RGB] [vCount left RGB]
    // Calibration (startCorner/clockwise/ledTop…) is read from Config::instance().
    void applyAmbilight(const uint8_t* data, uint8_t hCount, uint8_t vCount);

    // Light up each side in a distinct color so the user can see the strip layout.
    // The very first LED of the strip is shown white (= strip start marker).
    void showCalibrationPreview(uint8_t startCorner, bool clockwise,
                                uint8_t ledTop, uint8_t ledRight,
                                uint8_t ledBottom, uint8_t ledLeft);

    // Called once per loop iteration.
    void update();

    uint16_t numLeds()    const { return _numLeds; }
    uint8_t  brightness() const { return _brightness; }

private:
    LEDController() = default;

    // Interpolate srcCount RGB samples → dstCount CRGB entries (linear).
    static void interpolateSide(const uint8_t* src, uint8_t srcCount,
                                CRGB* dst, uint8_t dstCount);

    // Fill _leds[] from per-side CRGB arrays using startCorner + clockwise mapping.
    // Side natural directions: TOP[0]=top-left, RIGHT[0]=top-right,
    //                          BOTTOM[0]=bottom-right, LEFT[0]=bottom-left.
    void fillStrip(const CRGB* topC,    const CRGB* rightC,
                   const CRGB* bottomC, const CRGB* leftC,
                   uint8_t startCorner, bool clockwise,
                   uint8_t ledTop, uint8_t ledRight,
                   uint8_t ledBottom, uint8_t ledLeft);

    static CRGB _leds[MAX_LEDS];
    uint16_t    _numLeds    = 0;
    uint8_t     _brightness = 100;
};
