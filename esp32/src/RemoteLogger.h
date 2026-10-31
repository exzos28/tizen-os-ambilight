#pragma once
#include <Arduino.h>
#include <WiFiServer.h>
#include <WiFiClient.h>

class RemoteLogger : public Print {
public:
    static RemoteLogger& instance();

    void begin(uint16_t port = 2323);
    void handle();

    size_t write(uint8_t b)                        override;
    size_t write(const uint8_t* buf, size_t size)  override;

private:
    RemoteLogger() = default;
    void flushLine();

    static constexpr size_t LINE_BUF = 256;
    WiFiServer* _server = nullptr;
    WiFiClient  _client;
    uint8_t     _line[LINE_BUF];
    size_t      _lineLen = 0;
};

// Global convenience reference — use Log.print() / Log.printf() everywhere.
extern RemoteLogger& Log;
