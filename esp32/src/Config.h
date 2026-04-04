#pragma once
#include <Arduino.h>
#include <Preferences.h>

class Config {
public:
    static Config& instance();

    void begin();
    void save();
    void reset();   // wipes all NVS keys (factory reset)

    String   wifiSSID;
    String   wifiPassword;
    String   otaHostname;
    String   otaPassword;
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

private:
    Config() = default;
    Preferences _prefs;
};
