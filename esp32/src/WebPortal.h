#pragma once
#include <Arduino.h>
#include <WebServer.h>
#include "generated/ProtocolLimits.h"

class WebPortal {
public:
    static WebPortal& instance();

    void begin();
    void handle();

private:
    WebPortal() : _server(protocol::PORT_ESP32_HTTP) {}

    WebServer _server;
    uint32_t  _restartRequestedMs = 0;
    bool      _restartPending     = false;

    // Registers a handler that first adds the CORS header (Access-Control-Allow-Origin: *).
    void route(const char* uri, HTTPMethod method, void (WebPortal::*handler)());
    void allowCors();

    void handleNotFound();          // OPTIONS -> 204 (CORS preflight), anything else -> captive-portal redirect
    void redirectToRoot();
    void handleRoot();
    void handleStatus();
    void handleConfig();
    void handleMetrics();           // Heap, uptime, CPU freq
    void handleSaveNetwork();       // WiFi + OTA — saves and restarts
    void handleSaveLeds();          // Brightness + color — applies immediately
    void handleSaveCalibration();   // LED counts per side + layout — saves and applies
    void handlePreviewCalibration();// Temporary LED preview (does not save)
    void handleSaveMode();          // Switch plain/dynamic mode
    void handleSavePower();         // Lighting on/off
    void handleIdentify();          // Blink the strip white (not saved)
    void handleRestart();           // Reply, then restart
};
