#pragma once
#include <Arduino.h>
#include <WiFiUDP.h>

// Listens for UDP broadcast discovery probes from the TV service.
// Protocol: TV broadcasts "AMBILIGHT_DISCOVER" on DISCOVERY_PORT.
// We reply with "AMBILIGHT_DISCOVER:<our_ip>" so the TV sends
// ambilight frames directly to this ESP32 on UDP_PORT.
class DiscoveryResponder {
public:
    static DiscoveryResponder& instance();

    void begin();
    void handle();   // call every loop iteration

private:
    DiscoveryResponder() = default;

    WiFiUDP  _udp;
    bool     _started = false;

    static constexpr uint16_t DISCOVERY_PORT  = 9003;
    static constexpr const char* MAGIC        = "AMBILIGHT_DISCOVER";
};
