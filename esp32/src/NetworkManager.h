#pragma once
#include <Arduino.h>
#include <WiFi.h>
#include <DNSServer.h>

// Preferred last octet for the static IP (e.g. 192.168.x.245).
// The first three octets are learned from the router via DHCP.
// Set to 0 to disable and always use DHCP.
constexpr uint8_t PREFERRED_IP_LAST_OCTET = 245;

class NetworkManager {
public:
    static NetworkManager& instance();

    // Tries to connect to WiFi. Returns true on success.
    bool connectSTA(const String& ssid, const String& password,
                    uint32_t timeoutMs = 12000);

    // Starts an open access point + captive-portal DNS.
    // retryStation: saved Wi-Fi credentials exist, so the AP is only a fallback
    // (e.g. the router was still booting after a power cut). The device then
    // restarts to retry the station connection once no client has been connected
    // to the AP for AP_RETRY_MS.
    void startAP(const String& ssid, bool retryStation);

    // Must be called every loop() iteration when in AP mode.
    void handle();

    bool   isConnected() const;
    String ipAddress()   const;
    String mode()        const;   // "STA" | "AP"

private:
    static constexpr uint32_t AP_RETRY_MS = 120000;

    NetworkManager() = default;
    bool      _apMode       = false;
    bool      _retryStation = false;
    uint32_t  _apIdleSince  = 0;   // last time the AP had a client (or AP start)
    DNSServer _dns;
};
