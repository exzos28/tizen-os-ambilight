#pragma once
#include <Arduino.h>
#include <WiFiUDP.h>

constexpr uint16_t UDP_PORT = 9001;

// Listens on UDP_PORT for ambilight color frames and drives the LED strip.
// Packet format: [hCount: 1B][vCount: 1B][top hCount*3 B][right vCount*3 B]
//                [bottom hCount*3 B][left vCount*3 B]
// Frames are only applied when Config::ledMode == 1 (dynamic).
class UDPReceiver {
public:
    static UDPReceiver& instance();

    void begin();
    void handle();   // call every loop iteration

private:
    UDPReceiver() = default;

    WiFiUDP  _udp;
    bool     _started = false;

    // Max packet: 2 header + (255+255)*2*3 body = 3062 bytes — cap to 512 for ESP32 safety
    static constexpr uint16_t BUF_SIZE = 512;
    // TV service uses TargetCaptureW/H points (currently 4/3). Cap at 32 to reject garbage.
    static constexpr uint8_t  MAX_SIDE_COUNT = 32;
    uint8_t  _buf[BUF_SIZE];
    uint32_t _lastLogMs = 0;
};
