#include "RemoteLogger.h"

RemoteLogger& RemoteLogger::instance() {
    static RemoteLogger inst;
    return inst;
}

// Global reference — safe because instance() uses a function-local static.
RemoteLogger& Log = RemoteLogger::instance();

void RemoteLogger::begin(uint16_t port) {
    _server = new WiFiServer(port);
    _server->begin();
    Serial.printf("[Log] Telnet log on port %u\n", port);
}

void RemoteLogger::handle() {
    if (!_server) return;
    if (_client && _client.connected()) return;
    _client = _server->accept();
    if (_client && _client.connected())
        println("[Log] Remote connected.");
}

size_t RemoteLogger::write(uint8_t b) {
    if (b == '\n') {
        Serial.write('\r');
        if (_client && _client.connected()) _client.write('\r');
    }
    Serial.write(b);
    if (_client && _client.connected()) _client.write(b);
    return 1;
}

size_t RemoteLogger::write(const uint8_t* buf, size_t size) {
    for (size_t i = 0; i < size; i++) write(buf[i]);
    return size;
}
