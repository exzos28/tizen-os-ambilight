#include "DiscoveryResponder.h"
#include "RemoteLogger.h"
#include <WiFi.h>

DiscoveryResponder& DiscoveryResponder::instance() {
    static DiscoveryResponder inst;
    return inst;
}

void DiscoveryResponder::begin() {
    if (_udp.begin(DISCOVERY_PORT)) {
        _started = true;
        Log.printf("[Discovery] Listening for discovery probes on port %d\n", DISCOVERY_PORT);
    } else {
        Log.println("[Discovery] Failed to bind discovery socket");
    }
}

void DiscoveryResponder::handle() {
    if (!_started) return;

    int size = _udp.parsePacket();
    if (size <= 0) return;

    char buf[32] = {};
    int n = _udp.read(buf, sizeof(buf) - 1);
    if (n <= 0) return;

    if (strncmp(buf, MAGIC, strlen(MAGIC)) != 0) return;

    // Reply: "AMBILIGHT_DISCOVER:<our_ip>"
    String reply = String(MAGIC) + ":" + WiFi.localIP().toString();
    _udp.beginPacket(_udp.remoteIP(), _udp.remotePort());
    _udp.write((const uint8_t*)reply.c_str(), reply.length());
    _udp.endPacket();

    Log.printf("[Discovery] Replied to %s → %s\n",
               _udp.remoteIP().toString().c_str(), reply.c_str());
}
