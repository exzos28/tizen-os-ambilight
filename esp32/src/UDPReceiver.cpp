#include "UDPReceiver.h"
#include <stdarg.h>
#include "Config.h"
#include "LEDController.h"
#include "LightOutput.h"
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

    // Drain the socket and keep only the newest *valid* frame: if frames queued up
    // while the loop was busy, applying them one per iteration would add latency.
    // Each datagram is validated in a scratch buffer, so a malformed packet can't
    // overwrite a good frame that arrived before it.
    int       n = 0;
    IPAddress remote;
    for (int i = 0; i < MAX_PACKETS_PER_LOOP; i++) {
        int size = _udp.parsePacket();
        if (size <= 0) break;
        IPAddress from = _udp.remoteIP();

        if (size < 2 || size > (int)BUF_SIZE) {
            logThrottled("[UDP] Packet from %s bad size=%d\n", from.toString().c_str(), size);
            _udp.flush();
            continue;
        }

        int len = _udp.read(_scratch, BUF_SIZE);
        if (len < 2) continue;

        uint8_t hCount = _scratch[0];
        uint8_t vCount = _scratch[1];
        if (hCount > MAX_SIDE_COUNT || vCount > MAX_SIDE_COUNT) {
            logThrottled("[UDP] Packet from %s rejected: h=%d v=%d exceeds max=%d\n",
                         from.toString().c_str(), hCount, vCount, MAX_SIDE_COUNT);
            continue;
        }
        int expected = 2 + (hCount + vCount) * 2 * 3;
        if (len < expected) {
            logThrottled("[UDP] Packet from %s too short: got=%d expected=%d h=%d v=%d\n",
                         from.toString().c_str(), len, expected, hCount, vCount);
            continue;
        }

        memcpy(_buf, _scratch, len);
        n      = len;
        remote = from;
        _framesRx++;
        _lastRxMs = millis();
        _anyFrame = true;
    }
    if (n == 0) return;

    // Only apply frames with the lighting on, in dynamic mode, and not while a calibration
    // preview is active.
    auto& cfg     = Config::instance();
    bool  preview = LEDController::instance().previewActive();
    if (!LightOutput::acceptsFrames(cfg.power, cfg.ledMode, preview)) {
        logThrottled("[UDP] Frames from %s ignored (power=%d mode=%d preview=%d)\n",
                     remote.toString().c_str(), cfg.power ? 1 : 0, cfg.ledMode, preview ? 1 : 0);
        return;
    }

    // Log every ~5 seconds (avoid spam at frame rate).
    uint32_t now = millis();
    if (now - _lastLogMs >= 5000) {
        _lastLogMs = now;
        Log.printf("[UDP] Frame from %s h=%d v=%d bytes=%d\n",
                   remote.toString().c_str(), _buf[0], _buf[1], n);
    }

    LEDController::instance().applyAmbilight(_buf + 2, _buf[0], _buf[1]);
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
