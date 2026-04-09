#include "LEDController.h"
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
//     Log.printf("[LED] begin: driving %d LEDs at brightness %d\n", _numLeds, _brightness);
}

void LEDController::setNumLeds(uint16_t numLeds) {
    uint16_t newCount = min(numLeds, static_cast<uint16_t>(MAX_LEDS));

    // Clear at least the full calibrated strip length, not just _numLeds.
    // showCalibrationPreview() temporarily drives more LEDs than _numLeds
    // and then restores _numLeds without sending a blank frame, so those
    // extra LEDs stay physically lit. Using the config total guarantees they go dark.
    auto& cfg = Config::instance();
    uint16_t calTotal = (uint16_t)(cfg.ledTop + cfg.ledRight + cfg.ledBottom + cfg.ledLeft);
    uint16_t clearCount = max({_numLeds, calTotal, newCount});
    clearCount = min(clearCount, static_cast<uint16_t>(MAX_LEDS));

    Log.printf("[LED] setNumLeds: %d -> %d (clearing %d)\n", _numLeds, newCount, clearCount);
    fill_solid(_leds, clearCount, CRGB::Black);
    FastLED[0].setLeds(_leds, clearCount);
    FastLED.show();

    _numLeds = newCount;
    FastLED[0].setLeds(_leds, _numLeds);
    FastLED.show();
    Log.printf("[LED] setNumLeds done: driving %d LEDs\n", _numLeds);
}

void LEDController::setBrightness(uint8_t brightness) {
    _brightness = brightness;
    FastLED.setBrightness(brightness);
    FastLED.show();
}

void LEDController::setColor(uint8_t r, uint8_t g, uint8_t b) {
    Log.printf("[LED] setColor: rgb(%d,%d,%d) x %d LEDs\n", r, g, b, _numLeds);
    fill_solid(_leds, _numLeds, CRGB(r, g, b));
    // logLedDump("setColor→strip", _leds, _numLeds);
    FastLED.show();
}

void LEDController::setColors(const CRGB* colors, uint16_t count) {
    uint16_t n = min(count, _numLeds);
    memcpy(_leds, colors, n * sizeof(CRGB));
    FastLED.show();
}

// ---------------------------------------------------------------------------
// Private helpers
// ---------------------------------------------------------------------------

void LEDController::logLedDump(const char* tag, const CRGB* buf, uint16_t count, uint8_t /*n*/) {
    Log.printf("[LED] %s count=%d:\n", tag, count);
    for (uint16_t i = 0; i < count; i++)
        Log.printf("  [%3d] rgb(%3d,%3d,%3d)\n", i, buf[i].r, buf[i].g, buf[i].b);
}

void LEDController::interpolateSide(const uint8_t* src, uint8_t srcCount,
                                    CRGB* dst, uint8_t dstCount) {
    if (dstCount == 0 || srcCount == 0) return;
    if (srcCount == 1) {
        dst[0] = CRGB(src[0], src[1], src[2]);
        for (uint8_t i = 1; i < dstCount; i++) dst[i] = dst[0];
        return;
    }
    for (uint8_t i = 0; i < dstCount; i++) {
        float   t    = (float)i / (dstCount - 1) * (srcCount - 1);
        uint8_t lo   = (uint8_t)t;
        uint8_t hi   = (lo + 1 < srcCount) ? lo + 1 : lo;
        uint8_t frac = (uint8_t)((t - lo) * 255.0f);

        CHSV hsv_lo = rgb2hsv_approximate(CRGB(src[lo*3], src[lo*3+1], src[lo*3+2]));
        CHSV hsv_hi = rgb2hsv_approximate(CRGB(src[hi*3], src[hi*3+1], src[hi*3+2]));

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

// Fills _leds[] using per-side CRGB arrays and calibration.
//
// Side natural directions (index 0 = where the strip would START if coming from that corner CW):
//   TOP[0]    = top-left   corner
//   RIGHT[0]  = top-right  corner
//   BOTTOM[0] = bottom-right corner
//   LEFT[0]   = bottom-left corner
//
// Clockwise traversal from startCorner:
//   TL(0): TOP, RIGHT, BOTTOM, LEFT  (each in natural order)
//   TR(1): RIGHT, BOTTOM, LEFT, TOP
//   BR(2): BOTTOM, LEFT, TOP, RIGHT
//   BL(3): LEFT, TOP, RIGHT, BOTTOM
//
// Counter-clockwise traversal: same side rotation but each side reversed.
//   TL(0) CCW: LEFT, BOTTOM, RIGHT, TOP  (each reversed)
void LEDController::fillStrip(CRGB* dst,
                               const CRGB* topC,    const CRGB* rightC,
                               const CRGB* bottomC, const CRGB* leftC,
                               uint8_t startCorner, bool clockwise,
                               uint8_t ledTop, uint8_t ledRight,
                               uint8_t ledBottom, uint8_t ledLeft) {
    const CRGB* sides[4]  = { topC, rightC, bottomC, leftC };
    const uint8_t cnt[4]  = { ledTop, ledRight, ledBottom, ledLeft };

    uint16_t idx = 0;

    if (clockwise) {
        for (int s = 0; s < 4; s++) {
            int si = (startCorner + s) % 4;
            for (uint8_t i = 0; i < cnt[si]; i++) {
                if (idx >= _numLeds) return;
                dst[idx++] = sides[si][i];
            }
        }
    } else {
        // CCW: rotate sides in reverse, traverse each side reversed.
        // First side for CCW from corner c = (c+3)%4, then (c+2)%4, etc.
        for (int s = 0; s < 4; s++) {
            int si = (startCorner + 3 - s) % 4;
            for (int i = cnt[si] - 1; i >= 0; i--) {
                if (idx >= _numLeds) return;
                dst[idx++] = sides[si][i];
            }
        }
    }
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

    const uint8_t* topSrc    = data;
    const uint8_t* rightSrc  = topSrc   + hCount * 3;
    const uint8_t* bottomSrc = rightSrc + vCount * 3;
    const uint8_t* leftSrc   = bottomSrc + hCount * 3;

    // Interpolate each side's samples to the configured LED counts.
    // Static to avoid consuming ~4.5 KB of the ESP32 task stack.
    static CRGB topC   [MAX_LEDS / 4 + 1];
    static CRGB rightC [MAX_LEDS / 4 + 1];
    static CRGB bottomC[MAX_LEDS / 4 + 1];
    static CRGB leftC  [MAX_LEDS / 4 + 1];

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

    uint32_t now = millis();
    static uint32_t _lastAmbilightLogMs = 0;
    if (now - _lastAmbilightLogMs >= 1000) {
        _lastAmbilightLogMs = now;
        uint16_t total = cfg.ledTop + cfg.ledRight + cfg.ledBottom + cfg.ledLeft;
        // logLedDump("target→strip", _target, total);
    }

    _lastFrameMs = now;
}

// ---------------------------------------------------------------------------
// Calibration preview
// ---------------------------------------------------------------------------

void LEDController::showCalibrationPreview(uint8_t startCorner, bool clockwise,
                                            uint8_t ledTop, uint8_t ledRight,
                                            uint8_t ledBottom, uint8_t ledLeft) {
    _previewUntil = millis() + PREVIEW_HOLD_MS;

    const char* cornerNames[] = { "TL", "TR", "BR", "BL" };
    Log.printf("[Calib] corner=%s(%d) dir=%s top=%d right=%d bottom=%d left=%d\n",
               cornerNames[startCorner], startCorner,
               clockwise ? "CW" : "CCW",
               ledTop, ledRight, ledBottom, ledLeft);

    // Each side gets a distinct color.
    const CRGB sideColors[4] = {
        CRGB(255,   0,   0),   // TOP    — red
        CRGB(  0, 255,   0),   // RIGHT  — green
        CRGB(255, 255,   0),   // BOTTOM — yellow
        CRGB(  0,   0, 255),   // LEFT   — blue
    };
    const char* sideNames[]  = { "TOP(red)", "RIGHT(green)", "BOTTOM(yellow)", "LEFT(blue)" };

    static CRGB topC   [MAX_LEDS / 4 + 1];
    static CRGB rightC [MAX_LEDS / 4 + 1];
    static CRGB bottomC[MAX_LEDS / 4 + 1];
    static CRGB leftC  [MAX_LEDS / 4 + 1];

    fill_solid(topC,    ledTop,    sideColors[0]);
    fill_solid(rightC,  ledRight,  sideColors[1]);
    fill_solid(bottomC, ledBottom, sideColors[2]);
    fill_solid(leftC,   ledLeft,   sideColors[3]);

    uint16_t total = ledTop + ledRight + ledBottom + ledLeft;
    if (total == 0) return;

    uint16_t prevNum = _numLeds;
    _numLeds = min(static_cast<uint16_t>(total), static_cast<uint16_t>(MAX_LEDS));
    FastLED[0].setLeds(_leds, _numLeds);

    fillStrip(_leds, topC, rightC, bottomC, leftC,
              startCorner, clockwise,
              ledTop, ledRight, ledBottom, ledLeft);

    // Log the resulting strip layout: which LEDs got which side's color.
    const CRGB* sideArrays[4] = { topC, rightC, bottomC, leftC };
    uint8_t     sideCounts[4] = { ledTop, ledRight, ledBottom, ledLeft };
    uint16_t pos = 0;
    if (clockwise) {
        for (int s = 0; s < 4; s++) {
            int si = (startCorner + s) % 4;
            if (sideCounts[si] > 0)
                Log.printf("[Calib]   LED %3d..%3d → %s\n",
                           pos, pos + sideCounts[si] - 1, sideNames[si]);
            pos += sideCounts[si];
        }
    } else {
        for (int s = 0; s < 4; s++) {
            int si = (startCorner + 3 - s) % 4;
            if (sideCounts[si] > 0)
                Log.printf("[Calib]   LED %3d..%3d → %s (reversed)\n",
                           pos, pos + sideCounts[si] - 1, sideNames[si]);
            pos += sideCounts[si];
        }
    }

    // LED #0 = white, helps locate the physical start of the strip.
    _leds[0] = CRGB::White;

    FastLED.show();

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

    // In Ambilight mode, handle timeout (Auto-Off) and smooth blending.
    if (cfg.ledMode == 1 && !previewActive()) {
        // If no frames received for a long time, fade the target to black.
        if (now - _lastFrameMs > AUTO_OFF_TIMEOUT_MS) {
            fill_solid(_target, _numLeds, CRGB::Black);
        }

        // Smoothly blend current LED state toward the target.
        bool changed = false;
        for (uint16_t i = 0; i < _numLeds; i++) {
            if (_leds[i] != _target[i]) {
                _leds[i] = blend(_leds[i], _target[i], SMOOTH_ALPHA);
                changed = true;
            }
        }

        if (changed) {
            FastLED.show();
        }
    }
}
