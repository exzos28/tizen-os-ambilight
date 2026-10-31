#pragma once
#include <Arduino.h>
#include <Preferences.h>

class Config {
public:
    static Config& instance();

    void begin();
    void save();
    void reset();   // wipes all NVS keys (factory reset)

    // Clamps settings to their valid ranges. Runs on load and on save, so values
    // saved by older firmware or sent by a malformed request can't break output.
    void sanitize();

    String   wifiSSID;
    String   wifiPassword;
    String   otaHostname;
    String   otaPassword;
    // Static IP last octet in the router's subnet after connecting (0 = keep the DHCP
    // address). Valid: 0–254. Each controller on a network needs its own value.
    uint8_t  preferredLastOctet;
    uint16_t numLeds;
    uint8_t  brightness;
    uint8_t  colorR;
    uint8_t  colorG;
    uint8_t  colorB;

    // Calibration — LED count per side
    uint8_t  ledTop;
    uint8_t  ledRight;
    uint8_t  ledBottom;
    uint8_t  ledLeft;

    // Strip layout
    // startCorner: 0=top-left, 1=top-right, 2=bottom-right, 3=bottom-left
    uint8_t  startCorner;
    bool     clockwise;

    // Mode: 0=plain (static color), 1=dynamic (ambilight UDP)
    uint8_t  ledMode;

    // Advanced color settings
    float    gamma;          // 0.5 - 3.0, default 2.2
    float    saturation;     // 0.0 - 2.0, default 1.0
    uint8_t  whiteBalanceR;  // 0 - 255
    uint8_t  whiteBalanceG;  // 0 - 255
    uint8_t  whiteBalanceB;  // 0 - 255

    // Power limit: if the estimated strip power exceeds powerLimitW, the global brightness
    // is scaled down for that frame. 0 = no limit. pixelWatts = power of one LED pixel at
    // full white and brightness 255 (12 V WS2811, 60 LEDs/m ≈ 14.4 W/m / 20 pixels = 0.72).
    uint16_t powerLimitW;    // 0 - 2000
    float    pixelWatts;     // 0.05 - 5.0

private:
    Config() = default;
    Preferences _prefs;
};
