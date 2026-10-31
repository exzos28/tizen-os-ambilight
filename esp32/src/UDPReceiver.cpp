#include "UDPReceiver.h"
#include <stdarg.h>
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

    // Drain the socket and keep only the newest frame: if frames queued up while
    // the loop was busy, applying them one per iteration would add latency.
    int n = 0;
    IPAddress remote;
    for (int i = 0; i < MAX_PACKETS_PER_LOOP; i++) {
        int size = _udp.parsePacket();
        if (size <= 0) break;
        if (size < 2 || size > (int)BUF_SIZE) {
            logThrottled("[UDP] Packet from %s bad size=%d\n",
                         _udp.remoteIP().toString().c_str(), size);
            _udp.flush();
            continue;
        }
        remote = _udp.remoteIP();
        n = _udp.read(_buf, BUF_SIZE);
    }
    if (n < 2) return;

    // Only apply frames in dynamic mode, and not while a calibration preview is active.
    if (Config::instance().ledMode != 1 || LEDController::instance().previewActive()) {
        logThrottled("[UDP] Frames from %s ignored (mode=%d preview=%d)\n",
                     remote.toString().c_str(),
                     Config::instance().ledMode,
                     LEDController::instance().previewActive() ? 1 : 0);
        return;
    }

    uint8_t hCount = _buf[0];
    uint8_t vCount = _buf[1];

    if (hCount > MAX_SIDE_COUNT || vCount > MAX_SIDE_COUNT) {
        logThrottled("[UDP] Packet from %s rejected: h=%d v=%d exceeds max=%d\n",
                     remote.toString().c_str(), hCount, vCount, MAX_SIDE_COUNT);
        return;
    }

    // Validate expected payload length.
    int expected = 2 + (hCount + vCount) * 2 * 3;
    if (n < expected) {
        logThrottled("[UDP] Packet from %s too short: got=%d expected=%d h=%d v=%d\n",
                     remote.toString().c_str(), n, expected, hCount, vCount);
        return;
    }

    // Log every ~5 seconds (avoid spam at frame rate).
    uint32_t now = millis();
    if (now - _lastLogMs >= 5000) {
        _lastLogMs = now;
        Log.printf("[UDP] Frame from %s h=%d v=%d bytes=%d\n",
                   remote.toString().c_str(), hCount, vCount, n);
    }

    LEDController::instance().applyAmbilight(_buf + 2, hCount, vCount);
}

void UDPReceiver::logThrottled(const char* fmt, ...) {
    uint32_t now = millis();
    if (now - _lastWarnMs < 5000) return;
    _lastWarnMs = now;
    char line[160];
    va_list args;
    va_start(args, fmt);
    vsnprintf(line, sizeof(line), fmt, args);
    va_end(args);
    Log.print(line);
}
