#pragma once
#include <Arduino.h>
#include <WiFiUdp.h>

constexpr uint16_t UDP_PORT = 9001;

// Listens on UDP_PORT for ambilight color frames and drives the LED strip.
// Packet format: [hCount: 1B][vCount: 1B][top hCount*3 B][right vCount*3 B]
//                [bottom hCount*3 B][left vCount*3 B]
// Frames are only applied when the lighting is on and Config::ledMode == 1 (dynamic),
// and not during a calibration preview (LightOutput::acceptsFrames).
class UDPReceiver {
public:
    static UDPReceiver& instance();

    void begin();
    void handle();   // call every loop iteration

    // Valid frames received since boot (GET /metrics frames_rx): every datagram that passes
    // the size and side-count checks, including ones superseded by a newer queued frame and
    // ones ignored because of the mode, the power flag or a preview. Malformed datagrams and
    // ones the network stack dropped before the socket are not counted.
    uint32_t framesReceived() const { return _framesRx; }
    // ms since the last valid frame (GET /metrics last_frame_ms), -1 if none yet.
    int64_t  lastFrameAgeMs() const { return _anyFrame ? (int64_t)(millis() - _lastRxMs) : -1; }

private:
    UDPReceiver() = default;

    // Logs at most once per 5 s so a misbehaving sender can't flood the log.
    void logThrottled(const char* fmt, ...);

    WiFiUDP  _udp;
    bool     _started = false;

    // Max packet: 2 header + (255+255)*2*3 body = 3062 bytes — cap to 512 for ESP32 safety
    static constexpr uint16_t BUF_SIZE = 512;
    // TV service uses TargetCaptureW/H points (currently 4/3). Cap at 32 to reject garbage.
    static constexpr uint8_t  MAX_SIDE_COUNT = 32;
    uint8_t  _buf[BUF_SIZE];       // newest valid frame
    uint8_t  _scratch[BUF_SIZE];   // datagram being validated
    uint32_t _lastLogMs  = 0;
    uint32_t _lastWarnMs = 0;
    uint32_t _framesRx   = 0;
    uint32_t _lastRxMs   = 0;
    bool     _anyFrame   = false;
    // Upper bound on datagrams drained per loop() so a flood can't stall the loop.
    static constexpr int MAX_PACKETS_PER_LOOP = 16;
};
