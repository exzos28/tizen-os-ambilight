#include "Config.h"
#include "generated/ProtocolLimits.h"   // defaults and ranges, generated from protocol/

using namespace protocol;

Config& Config::instance() {
    static Config inst;
    return inst;
}

void Config::begin() {
    _prefs.begin("ambilight", false);
    wifiSSID     = _prefs.getString("wifi_ssid",  WIFI_SSID_DEFAULT);
    wifiPassword = _prefs.getString("wifi_pass",  WIFI_PASS_DEFAULT);
    otaHostname  = _prefs.getString("ota_host",   OTA_HOST_DEFAULT);
    otaPassword  = _prefs.getString("ota_pass",   OTA_PASS_DEFAULT);
    preferredLastOctet = _prefs.getUChar("ip_octet", IP_OCTET_DEFAULT);
    numLeds      = _prefs.getUShort("num_leds",    NUM_LEDS_DEFAULT);
    brightness   = _prefs.getUChar ("brightness",  BRIGHTNESS_DEFAULT);
    colorR       = _prefs.getUChar ("color_r",     (COLOR_DEFAULT_RGB >> 16) & 0xFF);
    colorG       = _prefs.getUChar ("color_g",     (COLOR_DEFAULT_RGB >> 8) & 0xFF);
    colorB       = _prefs.getUChar ("color_b",     COLOR_DEFAULT_RGB & 0xFF);
    ledTop       = _prefs.getUChar ("led_top",     LED_TOP_DEFAULT);
    ledRight     = _prefs.getUChar ("led_right",   LED_RIGHT_DEFAULT);
    ledBottom    = _prefs.getUChar ("led_bottom",  LED_BOTTOM_DEFAULT);
    ledLeft      = _prefs.getUChar ("led_left",    LED_LEFT_DEFAULT);
    startCorner  = _prefs.getUChar ("start_corner", START_CORNER_DEFAULT);
    clockwise    = _prefs.getBool  ("clockwise",   CLOCKWISE_DEFAULT);
    ledMode      = _prefs.getUChar ("led_mode",    LED_MODE_DEFAULT);
    power        = _prefs.getBool  ("power",       POWER_DEFAULT);
    gamma        = _prefs.getFloat ("gamma",       GAMMA_DEFAULT);
    saturation   = _prefs.getFloat ("saturation",  SATURATION_DEFAULT);
    whiteBalanceR = _prefs.getUChar ("wb_r",       WB_R_DEFAULT);
    whiteBalanceG = _prefs.getUChar ("wb_g",       WB_G_DEFAULT);
    whiteBalanceB = _prefs.getUChar ("wb_b",       WB_B_DEFAULT);
    powerLimitW   = _prefs.getUShort("pwr_limit",  POWER_LIMIT_W_DEFAULT);
    pixelWatts    = _prefs.getFloat ("px_watts",   PIXEL_WATTS_DEFAULT);
    sanitize();
}

void Config::sanitize() {
    if (!isfinite(gamma))      gamma = GAMMA_DEFAULT;
    if (!isfinite(saturation)) saturation = SATURATION_DEFAULT;
    if (!isfinite(pixelWatts)) pixelWatts = PIXEL_WATTS_DEFAULT;
    pixelWatts  = constrain(pixelWatts, PIXEL_WATTS_MIN, PIXEL_WATTS_MAX);
    powerLimitW = min(powerLimitW, POWER_LIMIT_W_MAX);
    gamma       = constrain(gamma,      GAMMA_MIN, GAMMA_MAX);
    saturation  = constrain(saturation, SATURATION_MIN, SATURATION_MAX);
    numLeds     = constrain(numLeds, NUM_LEDS_MIN, NUM_LEDS_MAX);
    if (startCorner > START_CORNER_MAX) startCorner = START_CORNER_DEFAULT;
    if (ledMode > LED_MODE_MAX)         ledMode = LED_MODE_DEFAULT;
    // Whether the address fits the network (router's own address, broadcast, subnet) is
    // checked at connect time against the real gateway and mask — see StaticIp.h.
    if (preferredLastOctet > IP_OCTET_MAX) preferredLastOctet = 0;
}

void Config::reset() {
    _prefs.begin("ambilight", false);
    _prefs.clear();
    _prefs.end();
}

void Config::save() {
    sanitize();
    _prefs.putString("wifi_ssid",   wifiSSID);
    _prefs.putString("wifi_pass",   wifiPassword);
    _prefs.putString("ota_host",    otaHostname);
    _prefs.putString("ota_pass",    otaPassword);
    _prefs.putUChar ("ip_octet",    preferredLastOctet);
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
    _prefs.putBool  ("power",       power);
    _prefs.putFloat ("gamma",       gamma);
    _prefs.putFloat ("saturation",  saturation);
    _prefs.putUChar ("wb_r",        whiteBalanceR);
    _prefs.putUChar ("wb_g",        whiteBalanceG);
    _prefs.putUChar ("wb_b",        whiteBalanceB);
    _prefs.putUShort("pwr_limit",   powerLimitW);
    _prefs.putFloat ("px_watts",    pixelWatts);
}
