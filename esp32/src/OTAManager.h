#pragma once
#include <Arduino.h>

class OTAManager {
public:
    static OTAManager& instance();

    void begin(const String& hostname, const String& password);
    void handle();

private:
    OTAManager() = default;
};
