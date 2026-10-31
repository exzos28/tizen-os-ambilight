#pragma once
#include <Arduino.h>
#include <WiFi.h>
#include <DNSServer.h>


class NetworkManager {
public:
    static NetworkManager& instance();

    // Tries to connect to WiFi. Returns true on success.
    // preferredLastOctet != 0: after DHCP, switch to the static address
    // <gateway subnet>.<preferredLastOctet> (not checked for conflicts); 0 keeps DHCP.
    bool connectSTA(const String& ssid, const String& password,
                    uint8_t preferredLastOctet, uint32_t timeoutMs = 12000);

    // Starts an open access point + captive-portal DNS.
    // If staSsid is not empty (saved credentials exist, so the AP is only a
    // fallback), the station interface keeps retrying that network in the
    // background — every STA_RETRY_INTERVAL_MS while nobody is connected to the
    // AP — and the device restarts in STA mode once it connects.
    void startAP(const String& ssid, const String& staSsid, const String& staPassword);

    // Must be called every loop() iteration when in AP mode.
    void handle();

    bool   isConnected() const;
    String ipAddress()   const;
    String mode()        const;   // "STA" | "AP"

private:
    static constexpr uint32_t STA_RETRY_INTERVAL_MS = 60000;
    static constexpr uint32_t STA_ATTEMPT_MS        = 15000;

    NetworkManager() = default;
    bool      _apMode          = false;
    String    _staSsid;
    String    _staPassword;
    uint32_t  _staAttemptStart = 0;   // 0 = no attempt running
    uint32_t  _nextStaAttempt  = 0;
    DNSServer _dns;
};
