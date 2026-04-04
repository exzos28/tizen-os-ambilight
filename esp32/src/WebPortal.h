#pragma once
#include <Arduino.h>
#include <WebServer.h>

class WebPortal {
public:
    static WebPortal& instance();

    void begin();
    void handle();

private:
    WebPortal() : _server(80) {}

    WebServer _server;

    void handleRoot();
    void handleStatus();
    void handleConfig();
    void handleMetrics();           // Heap, uptime, CPU freq
    void handleSaveNetwork();       // WiFi + OTA — saves and restarts
    void handleSaveLeds();          // Brightness + color — applies immediately
    void handleSaveCalibration();   // LED counts per side + layout — saves and applies
    void handlePreviewCalibration();// Temporary LED preview (does not save)
    void handleSaveMode();          // Switch plain/dynamic mode
};
