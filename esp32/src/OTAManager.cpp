#include "OTAManager.h"
#include "RemoteLogger.h"
#include <ArduinoOTA.h>

OTAManager& OTAManager::instance() {
    static OTAManager inst;
    return inst;
}

void OTAManager::begin(const String& hostname, const String& password) {
    ArduinoOTA.setHostname(hostname.c_str());
    ArduinoOTA.setPassword(password.c_str());

    ArduinoOTA.onStart([]() {
        Log.println("[OTA] Starting...");
    });
    ArduinoOTA.onEnd([]() {
        Log.println("[OTA] Done.");
    });
    ArduinoOTA.onProgress([](unsigned int done, unsigned int total) {
        Log.printf("[OTA] %u%%\n", done * 100 / total);
    });
    ArduinoOTA.onError([](ota_error_t err) {
        Log.printf("[OTA] Error %u\n", err);
    });

    ArduinoOTA.begin();
}

void OTAManager::handle() {
    ArduinoOTA.handle();
}
