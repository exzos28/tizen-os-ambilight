#include "NetworkManager.h"
#include "RemoteLogger.h"

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

    if (WiFi.status() == WL_CONNECTED && PREFERRED_IP_LAST_OCTET != 0) {
        // Build preferred IP from router's gateway prefix + our desired last octet.
        // e.g. gateway 192.168.1.1 → preferred IP 192.168.1.245
        IPAddress gw = WiFi.gatewayIP();
        IPAddress sn = WiFi.subnetMask();
        IPAddress preferred(gw[0], gw[1], gw[2], PREFERRED_IP_LAST_OCTET);
        WiFi.config(preferred, gw, sn);
    }

    _apMode = (WiFi.status() != WL_CONNECTED);
    return !_apMode;
}

void NetworkManager::startAP(const String& ssid, const String& staSsid,
                             const String& staPassword) {
    _staSsid     = staSsid;
    _staPassword = staPassword;

    // With saved credentials the AP is only a fallback (e.g. the router was still
    // booting after a power cut): keep the station interface up and retry it in
    // the background while the captive portal stays available.
    // Stop the failed boot-time attempt from auto-reconnecting on its own: it would
    // scan continuously and disturb AP clients. Retries are scheduled in handle().
    WiFi.setAutoReconnect(false);
    WiFi.disconnect(false, false);
    WiFi.persistent(false);   // retries must not rewrite the stored config in flash
    WiFi.mode(_staSsid.isEmpty() ? WIFI_AP : WIFI_AP_STA);
    WiFi.softAP(ssid.c_str());

    // Redirect every DNS query to our IP — this triggers the captive portal
    // popup on Android, iOS, Windows, and macOS automatically.
    _dns.start(53, "*", WiFi.softAPIP());

    _apMode          = true;
    _staAttemptStart = 0;
    _nextStaAttempt  = millis() + STA_RETRY_INTERVAL_MS;
}

void NetworkManager::handle() {
    if (!_apMode) return;
    _dns.processNextRequest();

    if (_staSsid.isEmpty()) return;

    if (WiFi.status() == WL_CONNECTED) {
        // Reboot once into plain STA mode so the preferred IP, OTA and the
        // services bound at startup all come up on the station interface.
        Log.println("[WiFi] Saved network is back — restarting in STA mode");
        delay(100);
        ESP.restart();
    }

    uint32_t now     = millis();
    bool     clients = WiFi.softAPgetStationNum() > 0;

    if (_staAttemptStart != 0) {
        // An attempt is running. Stop it when it times out, or as soon as someone
        // joins the AP — scanning for the router disturbs AP clients.
        if (clients || now - _staAttemptStart >= STA_ATTEMPT_MS) {
            WiFi.disconnect(false, false);
            _staAttemptStart = 0;
            _nextStaAttempt  = now + STA_RETRY_INTERVAL_MS;
        }
    } else if (!clients && (int32_t)(now - _nextStaAttempt) >= 0) {
        Log.println("[WiFi] Retrying the saved network...");
        WiFi.begin(_staSsid.c_str(), _staPassword.c_str());
        _staAttemptStart = now ? now : 1;
    }
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
