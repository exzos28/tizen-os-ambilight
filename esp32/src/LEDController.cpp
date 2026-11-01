#include "LEDController.h"
#include "StripLayout.h"
#include "PowerLimit.h"
#include "Config.h"
#include "RemoteLogger.h"

CRGB LEDController::_leds[MAX_LEDS];
CRGB LEDController::_target[MAX_LEDS];

LEDController& LEDController::instance() {
    static LEDController inst;
    return inst;
}

void LEDController::begin(uint16_t numLeds, uint8_t brightness) {
    _numLeds    = min(numLeds, static_cast<uint16_t>(MAX_LEDS));
    _brightness = brightness;

    FastLED.addLeds<WS2811, LED_PIN, BRG>(_leds, _numLeds);
    FastLED.setBrightness(_brightness);
    FastLED.clear(true);

    updateGammaTable(Config::instance().gamma);
}

void LEDController::setNumLeds(uint16_t numLeds) {
    uint16_t newCount = min(numLeds, static_cast<uint16_t>(MAX_LEDS));

    if (!redrawsNow()) {
        // Identify blink: it covers the new count from the next update() and clears
        // everything it lit when it ends.
        Log.printf("[LED] setNumLeds: %d -> %d (during identify)\n", _numLeds, newCount);
        _numLeds = newCount;
        FastLED[0].setLeds(_leds, _numLeds);
        return;
    }

    // Clear at least the full calibrated strip length, not just _numLeds.
    // showCalibrationPreview() temporarily drives more LEDs than _numLeds
    // and then restores _numLeds without sending a blank frame, so those
    // extra LEDs stay physically lit. Using the config total guarantees they go dark.
    uint16_t calTotal = layoutTotal();
    uint16_t clearCount = max({_numLeds, calTotal, newCount, _previewLit});
    clearCount = min(clearCount, static_cast<uint16_t>(MAX_LEDS));

    Log.printf("[LED] setNumLeds: %d -> %d (clearing %d)\n", _numLeds, newCount, clearCount);
    fill_solid(_leds, clearCount, CRGB::Black);
    FastLED[0].setLeds(_leds, clearCount);
    show();

    _previewLit = 0;
    _numLeds = newCount;
    FastLED[0].setLeds(_leds, _numLeds);
    show();
    Log.printf("[LED] setNumLeds done: driving %d LEDs\n", _numLeds);
}

void LEDController::setBrightness(uint8_t brightness) {
    _brightness = brightness;
    if (redrawsNow()) show();
}

void LEDController::refresh() {
    if (redrawsNow()) show();
}

void LEDController::setColor(uint8_t r, uint8_t g, uint8_t b) {
    if (!redrawsNow()) return;
    Log.printf("[LED] setColor: rgb(%d,%d,%d) x %d LEDs\n", r, g, b, _numLeds);
    fill_solid(_leds, _numLeds, CRGB(r, g, b));
    show();
}

void LEDController::setColors(const CRGB* colors, uint16_t count) {
    if (!redrawsNow()) return;
    uint16_t n = min(count, _numLeds);
    memcpy(_leds, colors, n * sizeof(CRGB));
    show();
}

LightOutput::Source LEDController::source() const {
    auto& cfg = Config::instance();
    return LightOutput::source(cfg.power, cfg.ledMode, _identifyActive, previewActive());
}

bool LEDController::redrawsNow() {
    if (LightOutput::redrawsNow(source())) return true;
    _identifyPhase = LightOutput::Identify::Phase::Done;   // re-applied by the next update()
    return false;
}

uint16_t LEDController::layoutTotal() {
    auto& cfg = Config::instance();
    return (uint16_t)(cfg.ledTop + cfg.ledRight + cfg.ledBottom + cfg.ledLeft);
}

void LEDController::repaint() {
    auto& cfg = Config::instance();

    // Frames aren't applied in this state (lighting off, plain mode, preview), so the
    // target is stale: drop it, or turning dynamic output back on would fade in an old
    // frame. The strip then stays dark until a fresh frame arrives.
    if (!LightOutput::acceptsFrames(cfg.power, cfg.ledMode, previewActive()))
        fill_solid(_target, MAX_LEDS, CRGB::Black);

    if (!redrawsNow()) return;   // identify: finishIdentify() repaints when the blink ends

    switch (source()) {
    case LightOutput::Source::Identify: break;   // not reached: redrawsNow() is false
    case LightOutput::Source::Preview:  drawPreview(); break;
    case LightOutput::Source::Plain:    setColor(cfg.colorR, cfg.colorG, cfg.colorB); break;
    case LightOutput::Source::Dark:
    case LightOutput::Source::Dynamic:  fill(CRGB::Black); break;   // update() blends frames in
    }
}

void LEDController::startIdentify() {
    Log.println("[LED] Identify: blinking white");
    _identifyActive = true;
    _identifyStart  = millis();
    _identifyPhase  = LightOutput::Identify::Phase::Done;   // forces a redraw on the next update()
}

void LEDController::drawIdentify(LightOutput::Identify::Phase phase) {
    _identifyPhase = phase;
    // Every LED that may be lit, including ones a longer calibration preview left lit
    // beyond the current count; never less than an earlier frame of this blink drove.
    uint16_t n = LightOutput::Identify::coverage(_numLeds, _previewLit, layoutTotal(), MAX_LEDS);
    _identifyLit = max(_identifyLit, n);
    fill_solid(_leds, _identifyLit,
               phase == LightOutput::Identify::Phase::On ? CRGB(CRGB::White) : CRGB(CRGB::Black));
    showLeds(_identifyLit);
}

void LEDController::finishIdentify() {
    // The blink ends on a dark slot, but a busy loop may have skipped drawing it: clear
    // everything the blink lit, including LEDs beyond the current count.
    uint16_t n = max(_identifyLit,
                     LightOutput::Identify::coverage(_numLeds, _previewLit, layoutTotal(), MAX_LEDS));
    _identifyActive = false;
    _identifyPhase  = LightOutput::Identify::Phase::Done;
    _identifyLit    = 0;
    fill_solid(_leds, n, CRGB::Black);
    showLeds(n);
    _previewLit = 0;   // covered by the clear above; a running preview sets it again
    // A preview that expired meanwhile is cleared (and repainted) by update() right after.
    if (!_previewShown || previewActive()) repaint();
}

// ---------------------------------------------------------------------------
// Private helpers
// ---------------------------------------------------------------------------

// Pushes _leds to the strip, applying the power limit: the estimated power of the frame
// (channel values are PWM duty, so power ≈ linear in them) is compared with the budget
// and, if over it, the global brightness is scaled down for this frame only.
//
// Not FastLED's setMaxPowerInMilliWatts: its power model stores milliwatts per channel in
// a uint8 (≤ 255 mW) tuned for 5 V WS2812, while a 12 V WS2811 pixel draws ~240 mW per
// channel or more. PowerLimit.h uses a per-pixel wattage the user takes from the datasheet.
void LEDController::fill(CRGB color) {
    fill_solid(_leds, _numLeds, color);
    show();
}

void LEDController::show() {
    showLeds(_numLeds);
}

void LEDController::showLeds(uint16_t count) {
    auto& cfg = Config::instance();
    count = min(count, static_cast<uint16_t>(MAX_LEDS));
    uint32_t sum = 0;
    for (uint16_t i = 0; i < count; i++) sum += _leds[i].r + _leds[i].g + _leds[i].b;

    PowerLimit::Result p = PowerLimit::apply(sum, _brightness, cfg.pixelWatts, cfg.powerLimitW);
    _estimatedWatts = p.watts;
    _powerLimited   = p.limited;

    if (count != _numLeds) FastLED[0].setLeds(_leds, count);
    FastLED.setBrightness(p.brightness);
    FastLED.show();
    if (count != _numLeds) FastLED[0].setLeds(_leds, _numLeds);
}

// One RGB sample of a UDP frame side (protocol::FRAME_BYTES_PER_SAMPLE bytes each).
static CRGB sampleAt(const uint8_t* src, uint8_t i) {
    const uint8_t* p = src + i * protocol::FRAME_BYTES_PER_SAMPLE;
    return CRGB(p[0], p[1], p[2]);
}

void LEDController::interpolateSide(const uint8_t* src, uint8_t srcCount,
                                    CRGB* dst, uint8_t dstCount) {
    if (dstCount == 0 || srcCount == 0) return;
    for (uint8_t i = 0; i < dstCount; i++) {
        StripLayout::InterpPos p = StripLayout::interpPosition(i, dstCount, srcCount);
        uint8_t lo = p.lo, hi = p.hi, frac = p.frac;
        if (lo == hi) { dst[i] = sampleAt(src, lo); continue; }

        CHSV hsv_lo = rgb2hsv_approximate(sampleAt(src, lo));
        CHSV hsv_hi = rgb2hsv_approximate(sampleAt(src, hi));

        // Shortest-path hue interpolation to avoid spinning around the colour wheel.
        int16_t dh = (int16_t)hsv_hi.h - (int16_t)hsv_lo.h;
        if (dh >  128) dh -= 256;
        if (dh < -128) dh += 256;

        CHSV result;
        result.h = (uint8_t)((int16_t)hsv_lo.h + (int16_t)(dh * frac / 255));
        result.s = lerp8by8(hsv_lo.s, hsv_hi.s, frac);
        result.v = lerp8by8(hsv_lo.v, hsv_hi.v, frac);
        hsv2rgb_rainbow(result, dst[i]);
    }
}

// Fills dst[] from per-side colors following the calibration (start corner, direction,
// LEDs per side). Traversal order and side directions: see StripLayout.h.
void LEDController::fillStrip(CRGB* dst,
                               const CRGB* topC,    const CRGB* rightC,
                               const CRGB* bottomC, const CRGB* leftC,
                               uint8_t startCorner, bool clockwise,
                               uint8_t ledTop, uint8_t ledRight,
                               uint8_t ledBottom, uint8_t ledLeft) {
    const CRGB* sides[4] = { topC, rightC, bottomC, leftC };
    const uint8_t cnt[4] = { ledTop, ledRight, ledBottom, ledLeft };
    StripLayout::forEach(startCorner, clockwise, cnt, _numLeds,
                         [&](uint16_t idx, uint8_t side, uint8_t i) { dst[idx] = sides[side][i]; });
}

// ---------------------------------------------------------------------------
// Ambilight
// ---------------------------------------------------------------------------

void LEDController::updateGammaTable(float gamma) {
    if (gamma == _lastGamma) return;
    _lastGamma = gamma;
    for (int i = 0; i < 256; i++) {
        _gammaTable[i] = (uint8_t)(pow((float)i / 255.0, gamma) * 255.0 + 0.5);
    }
}

void LEDController::processColors(CRGB* leds, uint16_t count) {
    auto& cfg = Config::instance();
    updateGammaTable(cfg.gamma);

    for (uint16_t i = 0; i < count; i++) {
        CRGB& c = leds[i];

        // 1. Saturation Boost (HSV)
        if (cfg.saturation != 1.0f) {
            CHSV hsv = rgb2hsv_approximate(c);
            uint16_t s = (uint16_t)(hsv.s * cfg.saturation);
            hsv.s = (uint8_t)(s > 255 ? 255 : s);
            hsv2rgb_rainbow(hsv, c);
        }

        // 2. White Balance
        if (cfg.whiteBalanceR != 255) c.r = (uint8_t)((uint16_t)c.r * cfg.whiteBalanceR / 255);
        if (cfg.whiteBalanceG != 255) c.g = (uint8_t)((uint16_t)c.g * cfg.whiteBalanceG / 255);
        if (cfg.whiteBalanceB != 255) c.b = (uint8_t)((uint16_t)c.b * cfg.whiteBalanceB / 255);

        // 3. Gamma Correction
        c.r = _gammaTable[c.r];
        c.g = _gammaTable[c.g];
        c.b = _gammaTable[c.b];
    }
}

void LEDController::applyAmbilight(const uint8_t* data, uint8_t hCount, uint8_t vCount) {
    auto& cfg = Config::instance();

    constexpr uint8_t BPS = protocol::FRAME_BYTES_PER_SAMPLE;
    const uint8_t* topSrc    = data;
    const uint8_t* rightSrc  = topSrc    + hCount * BPS;
    const uint8_t* bottomSrc = rightSrc  + vCount * BPS;
    const uint8_t* leftSrc   = bottomSrc + hCount * BPS;

    // Interpolate each side's samples to the configured LED counts.
    // Static to avoid consuming ~3 KB of the ESP32 task stack.
    static CRGB topC   [MAX_LEDS_PER_SIDE];
    static CRGB rightC [MAX_LEDS_PER_SIDE];
    static CRGB bottomC[MAX_LEDS_PER_SIDE];
    static CRGB leftC  [MAX_LEDS_PER_SIDE];

    interpolateSide(topSrc,    hCount, topC,    cfg.ledTop);
    interpolateSide(rightSrc,  vCount, rightC,  cfg.ledRight);
    interpolateSide(bottomSrc, hCount, bottomC, cfg.ledBottom);
    interpolateSide(leftSrc,   vCount, leftC,   cfg.ledLeft);

    // Apply color corrections
    processColors(topC,    cfg.ledTop);
    processColors(rightC,  cfg.ledRight);
    processColors(bottomC, cfg.ledBottom);
    processColors(leftC,   cfg.ledLeft);

    fillStrip(_target, topC, rightC, bottomC, leftC,
              cfg.startCorner, cfg.clockwise,
              cfg.ledTop, cfg.ledRight, cfg.ledBottom, cfg.ledLeft);

    _lastFrameMs = millis();
}

// ---------------------------------------------------------------------------
// Calibration preview
// ---------------------------------------------------------------------------

void LEDController::showCalibrationPreview(uint8_t startCorner, bool clockwise,
                                            uint8_t ledTop, uint8_t ledRight,
                                            uint8_t ledBottom, uint8_t ledLeft) {
    _previewUntil = millis() + protocol::ESP32_CALIBRATION_PREVIEW_MS;
    _previewShown = true;
    _pvCorner = startCorner;  _pvClockwise = clockwise;
    _pvTop    = ledTop;       _pvRight     = ledRight;
    _pvBottom = ledBottom;    _pvLeft      = ledLeft;

    const char* cornerNames[] = { "TL", "TR", "BR", "BL" };
    Log.printf("[Calib] corner=%s(%d) dir=%s top=%d right=%d bottom=%d left=%d\n",
               cornerNames[startCorner], startCorner,
               clockwise ? "CW" : "CCW",
               ledTop, ledRight, ledBottom, ledLeft);

    // Log the resulting strip layout: which LEDs got which side's color.
    const char* sideNames[]  = { "TOP(red)", "RIGHT(green)", "BOTTOM(yellow)", "LEFT(blue)" };
    uint8_t  sideCounts[4] = { ledTop, ledRight, ledBottom, ledLeft };
    uint16_t pos = 0;
    for (uint8_t seg = 0; seg < 4; seg++) {
        uint8_t si = StripLayout::sideAt(startCorner, clockwise, seg);
        if (sideCounts[si] > 0)
            Log.printf("[Calib]   LED %3d..%3d → %s%s\n",
                       pos, pos + sideCounts[si] - 1, sideNames[si], clockwise ? "" : " (reversed)");
        pos += sideCounts[si];
    }

    // An identify blink keeps the strip; finishIdentify() draws the preview when it ends.
    if (redrawsNow()) drawPreview();
}

void LEDController::drawPreview() {
    // Each side gets a distinct color.
    const CRGB sideColors[4] = {
        CRGB(255,   0,   0),   // TOP    — red
        CRGB(  0, 255,   0),   // RIGHT  — green
        CRGB(255, 255,   0),   // BOTTOM — yellow
        CRGB(  0,   0, 255),   // LEFT   — blue
    };

    static CRGB topC   [MAX_LEDS_PER_SIDE];
    static CRGB rightC [MAX_LEDS_PER_SIDE];
    static CRGB bottomC[MAX_LEDS_PER_SIDE];
    static CRGB leftC  [MAX_LEDS_PER_SIDE];

    fill_solid(topC,    _pvTop,    sideColors[0]);
    fill_solid(rightC,  _pvRight,  sideColors[1]);
    fill_solid(bottomC, _pvBottom, sideColors[2]);
    fill_solid(leftC,   _pvLeft,   sideColors[3]);

    uint16_t total = _pvTop + _pvRight + _pvBottom + _pvLeft;
    if (total == 0) return;

    uint16_t prevNum = _numLeds;
    _numLeds = min(static_cast<uint16_t>(total), static_cast<uint16_t>(MAX_LEDS));
    _previewLit = max(_previewLit, _numLeds);   // cleared by setNumLeds() on expiry
    FastLED[0].setLeds(_leds, _numLeds);

    fillStrip(_leds, topC, rightC, bottomC, leftC,
              _pvCorner, _pvClockwise,
              _pvTop, _pvRight, _pvBottom, _pvLeft);

    // LED #0 = white, helps locate the physical start of the strip.
    _leds[0] = CRGB::White;

    show();

    // Restore numLeds if it changed (preview is temporary).
    if (_numLeds != prevNum) {
        _numLeds = prevNum;
        FastLED[0].setLeds(_leds, _numLeds);
    }
}

void LEDController::update() {
    auto& cfg = Config::instance();

    // Limit update rate to ~60 FPS (16ms) to keep transitions consistent.
    uint32_t now = millis();
    if (now - _lastUpdateMs < 16) return;
    _lastUpdateMs = now;

    // Identify blink: owns the strip until it ends. Redraws on phase changes and after
    // another redraw request (redrawsNow()), not every update: a FastLED.show() of a long
    // strip takes tens of ms.
    if (_identifyActive) {
        namespace Id = LightOutput::Identify;
        uint32_t elapsed = now - _identifyStart;
        switch (Id::step(elapsed, _identifyPhase)) {
        case Id::Step::Keep:   return;
        case Id::Step::Draw:   drawIdentify(Id::phase(elapsed)); return;
        case Id::Step::Finish: finishIdentify(); break;   // then the preview expiry below
        }
    }

    // When a calibration preview expires, clear it (it may have lit more LEDs
    // than numLeds) and bring back the current output — in plain mode or with the
    // lighting off nothing else would repaint the strip.
    if (_previewShown && !previewActive()) {
        _previewShown = false;
        setNumLeds(cfg.numLeds);
        repaint();
    }

    // In Ambilight mode, handle timeout (Auto-Off) and smooth blending.
    if (source() == LightOutput::Source::Dynamic) {
        // If no frames received for a long time, fade the target to black.
        if (now - _lastFrameMs > AUTO_OFF_TIMEOUT_MS) {
            fill_solid(_target, _numLeds, CRGB::Black);
        }

        // Smoothly blend current LED state toward the target.
        bool changed = false;
        for (uint16_t i = 0; i < _numLeds; i++) {
            if (_leds[i] != _target[i]) {
                CRGB prev = _leds[i];
                _leds[i] = blend(prev, _target[i], SMOOTH_ALPHA);
                // 8-bit blending can stall a step short of the target; snap to it
                // so the strip settles and FastLED.show() stops being called.
                if (_leds[i] == prev) _leds[i] = _target[i];
                changed = true;
            }
        }

        if (changed) {
            show();
        }
    }
}
