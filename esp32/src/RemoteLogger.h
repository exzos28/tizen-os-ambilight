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
    WiFiServer* _server = nullptr;
    WiFiClient  _client;
};

// Global convenience reference — use Log.print() / Log.printf() everywhere.
extern RemoteLogger& Log;
