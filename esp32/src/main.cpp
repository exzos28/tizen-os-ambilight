#include <Arduino.h>
#include "Config.h"
#include "NetworkManager.h"
#include "RemoteLogger.h"
#include "OTAManager.h"
#include "LEDController.h"
#include "WebPortal.h"
#include "UDPReceiver.h"

// GPIO 0 — BOOT button (active LOW, internal pull-up).
// Hold for FACTORY_RESET_HOLD_MS at boot to wipe all saved config.
constexpr uint8_t  FACTORY_RESET_PIN     = 0;
constexpr uint32_t FACTORY_RESET_HOLD_MS = 3000;

static void checkFactoryReset() {
    pinMode(FACTORY_RESET_PIN, INPUT_PULLUP);
    if (digitalRead(FACTORY_RESET_PIN) == HIGH) return;   // not pressed

    Serial.println("[Boot] BOOT held — hold 3 s for factory reset...");
    uint32_t start = millis();
    while (digitalRead(FACTORY_RESET_PIN) == LOW) {
        if (millis() - start >= FACTORY_RESET_HOLD_MS) {
            Serial.println("[Boot] Factory reset! Wiping config...");
            Config::instance().reset();
            Serial.println("[Boot] Done — restarting.");
            delay(500);
            ESP.restart();
        }
        delay(50);
    }
    Serial.println("[Boot] Released early — skipping reset.");
}

void setup() {
    Serial.begin(115200);
    Log.println("[Boot] Starting...");

    // 0. Factory reset check (hold BOOT button for 3 s).
    checkFactoryReset();

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

    Log.printf("[Boot] Ready — http://%s\n", net.ipAddress().c_str());
}

void loop() {
    NetworkManager::instance().handle();   // processes DNS in AP/captive-portal mode
    OTAManager::instance().handle();
    RemoteLogger::instance().handle();
    WebPortal::instance().handle();
    UDPReceiver::instance().handle();
    LEDController::instance().update();
}
