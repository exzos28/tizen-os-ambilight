#include "LEDController.h"
#include "Config.h"

CRGB LEDController::_leds[MAX_LEDS];

LEDController& LEDController::instance() {
    static LEDController inst;
    return inst;
}

void LEDController::begin(uint16_t numLeds, uint8_t brightness) {
    _numLeds    = min(numLeds, static_cast<uint16_t>(MAX_LEDS));
    _brightness = brightness;

    FastLED.addLeds<WS2812B, LED_PIN, GRB>(_leds, _numLeds);
    FastLED.setBrightness(_brightness);
    FastLED.clear(true);
}

void LEDController::setNumLeds(uint16_t numLeds) {
    // Clear all currently active LEDs before resizing so old ones go dark.
    fill_solid(_leds, _numLeds, CRGB::Black);
    FastLED.show();

    _numLeds = min(numLeds, static_cast<uint16_t>(MAX_LEDS));
    FastLED[0].setLeds(_leds, _numLeds);
    FastLED.show();
}

void LEDController::setBrightness(uint8_t brightness) {
    _brightness = brightness;
    FastLED.setBrightness(brightness);
    FastLED.show();
}

void LEDController::setColor(uint8_t r, uint8_t g, uint8_t b) {
    fill_solid(_leds, _numLeds, CRGB(r, g, b));
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

void LEDController::interpolateSide(const uint8_t* src, uint8_t srcCount,
                                    CRGB* dst, uint8_t dstCount) {
    if (dstCount == 0 || srcCount == 0) return;
    if (srcCount == 1) {
        CRGB c(src[0], src[1], src[2]);
        for (uint8_t i = 0; i < dstCount; i++) dst[i] = c;
        return;
    }
    for (uint8_t i = 0; i < dstCount; i++) {
        float t    = (float)i / (dstCount - 1) * (srcCount - 1);
        uint8_t lo = (uint8_t)t;
        uint8_t hi = (lo + 1 < srcCount) ? lo + 1 : lo;
        float frac = t - lo;
        dst[i].r = (uint8_t)(src[lo*3+0] + frac * ((int)src[hi*3+0] - src[lo*3+0]));
        dst[i].g = (uint8_t)(src[lo*3+1] + frac * ((int)src[hi*3+1] - src[lo*3+1]));
        dst[i].b = (uint8_t)(src[lo*3+2] + frac * ((int)src[hi*3+2] - src[lo*3+2]));
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
void LEDController::fillStrip(const CRGB* topC,    const CRGB* rightC,
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
                _leds[idx++] = sides[si][i];
            }
        }
    } else {
        // CCW: rotate sides in reverse, traverse each side reversed.
        // First side for CCW from corner c = (c+3)%4, then (c+2)%4, etc.
        for (int s = 0; s < 4; s++) {
            int si = (startCorner + 3 - s) % 4;
            for (int i = cnt[si] - 1; i >= 0; i--) {
                if (idx >= _numLeds) return;
                _leds[idx++] = sides[si][i];
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Ambilight
// ---------------------------------------------------------------------------

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

    fillStrip(topC, rightC, bottomC, leftC,
              cfg.startCorner, cfg.clockwise,
              cfg.ledTop, cfg.ledRight, cfg.ledBottom, cfg.ledLeft);
    FastLED.show();
}

// ---------------------------------------------------------------------------
// Calibration preview
// ---------------------------------------------------------------------------

void LEDController::showCalibrationPreview(uint8_t startCorner, bool clockwise,
                                            uint8_t ledTop, uint8_t ledRight,
                                            uint8_t ledBottom, uint8_t ledLeft) {
    _previewUntil = millis() + PREVIEW_HOLD_MS;

    // Each side gets a distinct color.
    // Using project's orange/green/blue/purple palette.
    const CRGB sideColors[4] = {
        CRGB(249, 115,  22),   // TOP    — orange
        CRGB( 34, 197,  94),   // RIGHT  — green
        CRGB( 14, 165, 233),   // BOTTOM — blue
        CRGB(168,  85, 247),   // LEFT   — purple
    };

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

    fillStrip(topC, rightC, bottomC, leftC,
              startCorner, clockwise,
              ledTop, ledRight, ledBottom, ledLeft);

    // Mark the very first strip LED white so the user can locate the start.
    _leds[0] = CRGB::White;

    FastLED.show();

    // Restore numLeds if it changed (preview is temporary).
    if (_numLeds != prevNum) {
        _numLeds = prevNum;
        FastLED[0].setLeds(_leds, _numLeds);
    }
}

void LEDController::update() {
    // Placeholder for future animation / UDP-driven updates.
}
