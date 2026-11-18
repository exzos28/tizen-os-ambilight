#pragma once
#include <Arduino.h>
#include <FastLED.h>
#include "ColorPipeline.h"
#include "LightOutput.h"
#include "generated/ProtocolLimits.h"

constexpr uint8_t  LED_PIN  = 23;
constexpr uint16_t MAX_LEDS = protocol::ESP32_MAX_LEDS;
constexpr uint8_t  MAX_LEDS_PER_SIDE = protocol::ESP32_MAX_LEDS_PER_SIDE;
static_assert(MAX_LEDS == protocol::NUM_LEDS_MAX, "num_leds range must match the LED buffer");
static_assert(protocol::LED_TOP_MAX <= MAX_LEDS_PER_SIDE && protocol::LED_RIGHT_MAX <= MAX_LEDS_PER_SIDE &&
              protocol::LED_BOTTOM_MAX <= MAX_LEDS_PER_SIDE && protocol::LED_LEFT_MAX <= MAX_LEDS_PER_SIDE,
              "side counts must fit the per-side buffers");

class LEDController {
public:
    static LEDController& instance();

    void begin(uint16_t numLeds, uint8_t brightness = protocol::BRIGHTNESS_DEFAULT);
    void setNumLeds(uint16_t numLeds);
    void setBrightness(uint8_t brightness);
    void setColor(uint8_t r, uint8_t g, uint8_t b);
    void setColors(const CRGB* colors, uint16_t count);
    // Re-sends the current pixels, e.g. after the power-limit settings changed.
    void refresh();

    // Redraws what the current state should show (LightOutput::source): the static color
    // in plain mode, dark when the lighting is off or in dynamic mode (frames then blend
    // back in), the calibration preview while it runs. Call after power or mode changes.
    // Also drops the dynamic target while frames aren't applied (LightOutput::acceptsFrames),
    // so dynamic mode stays dark until a fresh frame arrives.
    //
    // During an identify blink this and every other redraw (setNumLeds, setColor,
    // setBrightness, refresh, a preview) only update state: the blink re-applies its
    // current phase on the next update() and repaint()s when it ends.
    void repaint();

    // Blinks every LED that may be lit white (LightOutput::Identify), then repaint()s.
    // Non-blocking: driven by update(). Works with the lighting off.
    void startIdentify();

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
    // Estimated strip power of the last frame (after the power limit) and whether the
    // limit reduced it. See Config::powerLimitW / pixelWatts.
    float    estimatedWatts() const { return _estimatedWatts; }
    bool     powerLimited()   const { return _powerLimited; }
    uint8_t  brightness() const { return _brightness; }
    bool     previewActive() const { return millis() < _previewUntil; }
    bool     identifyActive() const { return _identifyActive; }
    LightOutput::Source source() const;

private:
    static constexpr uint32_t AUTO_OFF_TIMEOUT_MS = 5000;
    LEDController() = default;

    void show();   // FastLED.show() of the driven LEDs with the power limit applied
    // Like show(), but pushes the first `count` LEDs (count may exceed _numLeds); the
    // controller drives _numLeds again afterwards.
    void showLeds(uint16_t count);
    void fill(CRGB color);   // all driven LEDs, then show()
    void drawPreview();      // the calibration preview from the _pv* parameters

    // LightOutput::redrawsNow() for the current state. False during an identify blink: it
    // then marks the blink for a redraw on the next update(), and the caller only updates
    // state.
    bool redrawsNow();
    // Sum of the calibrated side counts (Config::ledTop + ledRight + ledBottom + ledLeft).
    static uint16_t layoutTotal();
    void drawIdentify(LightOutput::Identify::Phase phase);   // phase over Identify::coverage()
    void finishIdentify();   // clears what the blink lit and repaint()s, in one show

    // Rebuilds the output LUT (white balance, gamma) if those settings changed.
    void updateOutputLut();
    // The strip was drawn black elsewhere: the smoothing starts from black again, and each
    // LED's transition to the current target is decided anew (ColorPipeline::onFrame).
    void clearSmoothing();
    // ColorPipeline::onFrame for every driven LED against the current target.
    void decideTransitions(bool sceneCut);

    // Fill dst[] from per-side CRGB arrays using startCorner + clockwise mapping.
    // Side natural directions: TOP[0]=top-left, RIGHT[0]=top-right,
    //                          BOTTOM[0]=bottom-right, LEFT[0]=bottom-left.
    void fillStrip(CRGB* dst,
                   const CRGB* topC,    const CRGB* rightC,
                   const CRGB* bottomC, const CRGB* leftC,
                   uint8_t startCorner, bool clockwise,
                   uint8_t ledTop, uint8_t ledRight,
                   uint8_t ledBottom, uint8_t ledLeft);

    static CRGB _leds[MAX_LEDS];    // displayed (output: after white balance and gamma)
    // Latest decoded frame in strip order, screen-referred (saturation applied, before white
    // balance and gamma), after the dark threshold.
    static CRGB _target[MAX_LEDS];
    // Dynamic mode: what each LED shows, screen-referred in 8.8 fixed point, and its
    // dark-threshold and fast-transition flags (ColorPipeline::LedState).
    static ColorPipeline::LedState _state[MAX_LEDS];
    uint16_t    _numLeds      = 0;
    uint8_t     _brightness   = protocol::BRIGHTNESS_DEFAULT;
    uint32_t    _previewUntil = 0;
    bool        _previewShown = false;   // a preview was drawn and not yet cleared
    uint16_t    _previewLit   = 0;       // LEDs a preview lit beyond the current count
    // Last requested preview, redrawn when an identify blink ends during it.
    uint8_t     _pvCorner = 0, _pvTop = 0, _pvRight = 0, _pvBottom = 0, _pvLeft = 0;
    bool        _pvClockwise = true;
    bool        _identifyActive = false;
    uint32_t    _identifyStart  = 0;
    uint16_t    _identifyLit    = 0;   // LEDs the running blink has driven (may exceed _numLeds)
    // LEDs the next showLeds() pushes at least: set by finishIdentify() around its repaint().
    uint16_t    _pushAtLeast    = 0;
    // Phase last drawn by the blink; Done = nothing drawn yet or a redraw request since.
    LightOutput::Identify::Phase _identifyPhase = LightOutput::Identify::Phase::Done;
    float       _estimatedWatts = 0;
    bool        _powerLimited   = false;
    uint32_t    _lastFrameMs  = 0;
    uint32_t    _lastUpdateMs = 0;
    bool        _autoOff      = false;   // the target went dark after AUTO_OFF_TIMEOUT_MS
    uint8_t     _prevAvgLuma  = 0;       // average sample luma of the last frame (scene cuts)
    uint8_t     _gammaTable[256];
    ColorPipeline::OutputLut _lut;
    float       _lutGamma     = -1.0f;   // settings _lut was built for; -1 = not built
    uint8_t     _lutWb[3]     = {0, 0, 0};
};
