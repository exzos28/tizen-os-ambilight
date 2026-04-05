#include <Arduino.h>
#include <WiFi.h>
#include "Config.h"
#include "NetworkManager.h"
#include "RemoteLogger.h"
#include "OTAManager.h"
#include "LEDController.h"
#include "WebPortal.h"
#include "UDPReceiver.h"
#include "DiscoveryResponder.h"

// GPIO 0 — BOOT button (active LOW, internal pull-up).
// Hold for FACTORY_RESET_HOLD_MS at any time to wipe all saved config.
constexpr uint8_t  FACTORY_RESET_PIN     = 0;
constexpr uint32_t FACTORY_RESET_HOLD_MS = 3000;

static uint32_t _bootPressStart = 0;
static uint32_t _lastNetLogMs   = 0;

static void checkFactoryReset() {
    if (digitalRead(FACTORY_RESET_PIN) == HIGH) {
        _bootPressStart = 0;
        return;
    }
    if (_bootPressStart == 0) {
        _bootPressStart = millis();
        Serial.println("[Boot] BOOT held — hold 3 s for factory reset...");
        return;
    }
    if (millis() - _bootPressStart >= FACTORY_RESET_HOLD_MS) {
        Serial.println("[Boot] Factory reset! Wiping config...");
        Config::instance().reset();
        Serial.println("[Boot] Done — restarting.");
        delay(500);
        ESP.restart();
    }
}

void setup() {
    Serial.begin(115200);
    Log.println("[Boot] Starting...");

    pinMode(FACTORY_RESET_PIN, INPUT_PULLUP);

    // 1. Load persistent config from NVS.
    auto& cfg = Config::instance();
    cfg.begin();

    // 2. Connect to WiFi; fall back to AP mode if credentials are missing or wrong.
    auto& net = NetworkManager::instance();
    if (net.connectSTA(cfg.wifiSSID, cfg.wifiPassword)) {
        Log.printf("[WiFi] Connected — IP: %s\n", net.ipAddress().c_str());
    } else {
        Log.println("[WiFi] Could not connect — starting AP 'ambilight-setup'");
        Log.println("[WiFi] Open http://192.168.4.1 to configure WiFi.");
        net.startAP("ambilight-setup");
    }

    // 3. OTA updates (only meaningful in STA mode).
    OTAManager::instance().begin(cfg.otaHostname, cfg.otaPassword);

    // 4. Telnet remote log.
    RemoteLogger::instance().begin();

    // 5. LED strip.
    auto& leds = LEDController::instance();
    leds.begin(cfg.numLeds, cfg.brightness);
    leds.setColor(cfg.colorR, cfg.colorG, cfg.colorB);

    // 6. Web configuration portal.
    WebPortal::instance().begin();

    // 7. UDP receiver for ambilight frames.
    UDPReceiver::instance().begin();

    // 8. Discovery responder — lets the TV find this ESP32 automatically.
    DiscoveryResponder::instance().begin();

    Log.printf("[Boot] Ready — http://%s\n", net.ipAddress().c_str());
}

static void logNetStatus() {
    uint32_t now = millis();
    if (now - _lastNetLogMs < 10000) return;
    _lastNetLogMs = now;
    auto& net = NetworkManager::instance();
    Log.printf("[Net] mode=%s ip=%s rssi=%d dBm heap=%u\n",
               net.mode().c_str(),
               net.ipAddress().c_str(),
               WiFi.RSSI(),
               ESP.getFreeHeap());
}

void loop() {
    checkFactoryReset();
    logNetStatus();
    NetworkManager::instance().handle();   // processes DNS in AP/captive-portal mode
    OTAManager::instance().handle();
    RemoteLogger::instance().handle();
    WebPortal::instance().handle();
    UDPReceiver::instance().handle();
    DiscoveryResponder::instance().handle();
    LEDController::instance().update();
}
