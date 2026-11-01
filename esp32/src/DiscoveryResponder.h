#pragma once
#include <Arduino.h>
#include <WiFiUdp.h>

// Listens for UDP broadcast discovery probes from the TV service.
// Protocol: TV broadcasts protocol::DISCOVERY_MAGIC ("AMBILIGHT_DISCOVER") on
// protocol::PORT_DISCOVERY. We reply with "AMBILIGHT_DISCOVER:<our_ip>" so the TV sends
// ambilight frames directly to this ESP32 on protocol::PORT_FRAME.
class DiscoveryResponder {
public:
    static DiscoveryResponder& instance();

    void begin();
    void handle();   // call every loop iteration

private:
    DiscoveryResponder() = default;

    WiFiUDP  _udp;
    bool     _started = false;
};
