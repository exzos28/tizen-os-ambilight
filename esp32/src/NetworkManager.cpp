#include "NetworkManager.h"

NetworkManager& NetworkManager::instance() {
    static NetworkManager inst;
    return inst;
}

bool NetworkManager::connectSTA(const String& ssid, const String& password,
                                uint32_t timeoutMs) {
    if (ssid.isEmpty()) return false;

    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(true);
    WiFi.persistent(true);
    WiFi.setSleep(false);
    WiFi.begin(ssid.c_str(), password.c_str());

    uint32_t start = millis();
    while (WiFi.status() != WL_CONNECTED && millis() - start < timeoutMs) {
        delay(200);
    }

    _apMode = (WiFi.status() != WL_CONNECTED);
    return !_apMode;
}

void NetworkManager::startAP(const String& ssid) {
    WiFi.mode(WIFI_AP);
    WiFi.softAP(ssid.c_str());

    // Redirect every DNS query to our IP — this triggers the captive portal
    // popup on Android, iOS, Windows, and macOS automatically.
    _dns.start(53, "*", WiFi.softAPIP());

    _apMode = true;
}

void NetworkManager::handle() {
    if (_apMode) _dns.processNextRequest();
}

bool NetworkManager::isConnected() const {
    return WiFi.status() == WL_CONNECTED;
}

String NetworkManager::ipAddress() const {
    return _apMode ? WiFi.softAPIP().toString()
                   : WiFi.localIP().toString();
}

String NetworkManager::mode() const {
    return _apMode ? "AP" : "STA";
}
