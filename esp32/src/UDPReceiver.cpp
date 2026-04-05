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

    IPAddress remote = _udp.remoteIP();

    // Only apply frames in dynamic mode, and not while a calibration preview is active.
    if (Config::instance().ledMode != 1 || LEDController::instance().previewActive()) {
        Log.printf("[UDP] Packet from %s ignored (mode=%d preview=%d)\n",
                   remote.toString().c_str(),
                   Config::instance().ledMode,
                   LEDController::instance().previewActive() ? 1 : 0);
        _udp.flush();
        return;
    }

    if (size < 2 || size > (int)BUF_SIZE) {
        Log.printf("[UDP] Packet from %s bad size=%d\n", remote.toString().c_str(), size);
        _udp.flush();
        return;
    }

    int n = _udp.read(_buf, BUF_SIZE);
    if (n < 2) return;

    uint8_t hCount = _buf[0];
    uint8_t vCount = _buf[1];

    if (hCount > MAX_SIDE_COUNT || vCount > MAX_SIDE_COUNT) {
        Log.printf("[UDP] Packet from %s rejected: h=%d v=%d exceeds max=%d\n",
                   remote.toString().c_str(), hCount, vCount, MAX_SIDE_COUNT);
        return;
    }

    // Validate expected payload length.
    int expected = 2 + (hCount + vCount) * 2 * 3;
    if (n < expected) {
        Log.printf("[UDP] Packet from %s too short: got=%d expected=%d h=%d v=%d\n",
                   remote.toString().c_str(), n, expected, hCount, vCount);
        return;
    }

    // Log every ~5 seconds (avoid spam at 60fps).
    uint32_t now = millis();
    if (now - _lastLogMs >= 5000) {
        _lastLogMs = now;
        Log.printf("[UDP] Frame from %s h=%d v=%d bytes=%d\n",
                   remote.toString().c_str(), hCount, vCount, n);
    }

    LEDController::instance().applyAmbilight(_buf + 2, hCount, vCount);
}
