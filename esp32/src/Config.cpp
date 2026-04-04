#include "Config.h"

Config& Config::instance() {
    static Config inst;
    return inst;
}

void Config::begin() {
    _prefs.begin("ambilight", false);
    wifiSSID     = _prefs.getString("wifi_ssid",  "");
    wifiPassword = _prefs.getString("wifi_pass",  "");
    otaHostname  = _prefs.getString("ota_host",   "ambilight");
    otaPassword  = _prefs.getString("ota_pass",   "123456");
    numLeds      = _prefs.getUShort("num_leds",    300);
    brightness   = _prefs.getUChar ("brightness",  100);
    colorR       = _prefs.getUChar ("color_r",     255);
    colorG       = _prefs.getUChar ("color_g",     255);
    colorB       = _prefs.getUChar ("color_b",     255);
    ledTop       = _prefs.getUChar ("led_top",     10);
    ledRight     = _prefs.getUChar ("led_right",   6);
    ledBottom    = _prefs.getUChar ("led_bottom",  10);
    ledLeft      = _prefs.getUChar ("led_left",    6);
    startCorner  = _prefs.getUChar ("start_corner", 0);
    clockwise    = _prefs.getBool  ("clockwise",   true);
    ledMode      = _prefs.getUChar ("led_mode",    0);
}

void Config::reset() {
    _prefs.begin("ambilight", false);
    _prefs.clear();
    _prefs.end();
}

void Config::save() {
    _prefs.putString("wifi_ssid",   wifiSSID);
    _prefs.putString("wifi_pass",   wifiPassword);
    _prefs.putString("ota_host",    otaHostname);
    _prefs.putString("ota_pass",    otaPassword);
    _prefs.putUShort("num_leds",    numLeds);
    _prefs.putUChar ("brightness",  brightness);
    _prefs.putUChar ("color_r",     colorR);
    _prefs.putUChar ("color_g",     colorG);
    _prefs.putUChar ("color_b",     colorB);
    _prefs.putUChar ("led_top",     ledTop);
    _prefs.putUChar ("led_right",   ledRight);
    _prefs.putUChar ("led_bottom",  ledBottom);
    _prefs.putUChar ("led_left",    ledLeft);
    _prefs.putUChar ("start_corner", startCorner);
    _prefs.putBool  ("clockwise",   clockwise);
    _prefs.putUChar ("led_mode",    ledMode);
}
