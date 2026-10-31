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
    if (b == '\n') Serial.write('\r');
    Serial.write(b);

    // Telnet gets whole lines: one TCP write per line instead of per byte.
    if (b == '\n') {
        if (_lineLen < LINE_BUF - 1) _line[_lineLen++] = '\r';
        if (_lineLen < LINE_BUF)     _line[_lineLen++] = '\n';
        flushLine();
    } else {
        _line[_lineLen++] = b;
        if (_lineLen >= LINE_BUF - 2) flushLine();   // keep room for "\r\n"
    }
    return 1;
}

size_t RemoteLogger::write(const uint8_t* buf, size_t size) {
    for (size_t i = 0; i < size; i++) write(buf[i]);
    return size;
}

void RemoteLogger::flushLine() {
    if (_lineLen > 0 && _client && _client.connected())
        _client.write(_line, _lineLen);
    _lineLen = 0;
}
