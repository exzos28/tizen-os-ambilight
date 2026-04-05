#include "UDPReceiver.h"
#include "Config.h"
#include "LEDController.h"
#include "RemoteLogger.h"

UDPReceiver& UDPReceiver::instance() {
    static UDPReceiver inst;
    return inst;
}

void UDPReceiver::begin() {
    if (_udp.begin(UDP_PORT)) {
        _started = true;
        Log.printf("[UDP] Listening for ambilight frames on port %d\n", UDP_PORT);
    } else {
        Log.println("[UDP] Failed to bind UDP socket");
    }
}

void UDPReceiver::handle() {
    if (!_started) return;

    int size = _udp.parsePacket();
    if (size <= 0) return;

    // Only apply frames in dynamic mode, and not while a calibration preview is active.
    if (Config::instance().ledMode != 1 || LEDController::instance().previewActive()) {
        _udp.flush();
        return;
    }

    if (size < 2 || size > (int)BUF_SIZE) {
        _udp.flush();
        return;
    }

    int n = _udp.read(_buf, BUF_SIZE);
    if (n < 2) return;

    uint8_t hCount = _buf[0];
    uint8_t vCount = _buf[1];

    // Validate expected payload length.
    int expected = 2 + (hCount + vCount) * 2 * 3;
    if (n < expected) return;

    LEDController::instance().applyAmbilight(_buf + 2, hCount, vCount);
}
