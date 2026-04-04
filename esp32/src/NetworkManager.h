#pragma once
#include <Arduino.h>
#include <WiFi.h>
#include <DNSServer.h>

class NetworkManager {
public:
    static NetworkManager& instance();

    // Tries to connect to WiFi. Returns true on success.
    bool connectSTA(const String& ssid, const String& password,
                    uint32_t timeoutMs = 12000);

    // Starts an open access point + captive-portal DNS.
    void startAP(const String& ssid);

    // Must be called every loop() iteration when in AP mode.
    void handle();

    bool   isConnected() const;
    String ipAddress()   const;
    String mode()        const;   // "STA" | "AP"

private:
    NetworkManager() = default;
    bool      _apMode = false;
    DNSServer _dns;
};
