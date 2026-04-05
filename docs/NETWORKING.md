# Networking

```mermaid
sequenceDiagram
    participant UI as TV (UI)<br/>Tizen Web App
    participant SVC as TV (Service)<br/>C# ServiceApplication
    participant ESP as ESP32<br/>Firmware
    participant WEB as ESP32 (Web)<br/>WebPortal :80

    Note over UI,WEB: Service lifecycle

    UI->>SVC: tizen.application.launchAppControl<br/>(command=start)
    UI->>SVC: tizen.application.launchAppControl<br/>(command=stop)

    Note over SVC,ESP: Discovery — UDP Broadcast

    SVC->>ESP: UDP broadcast :9003<br/>"AMBILIGHT_DISCOVER"
    ESP-->>SVC: UDP unicast :9003<br/>"AMBILIGHT_DISCOVER:<esp_ip>"

    Note over SVC,ESP: Log events

    loop on each significant event
        SVC->>ESP: HTTP POST http://<esp_ip>:9000<br/>{"event":"...", "app":"Service", ...}
    end

    Note over SVC,ESP: Ambilight frames

    loop per frame
        SVC->>SVC: libvideoenhance.so<br/>HW RGB capture (14 points)
        SVC->>ESP: UDP unicast :9001<br/>[hCount][vCount][RGB × 14]
        ESP->>ESP: UDPReceiver → LEDController<br/>update WS2812B strip
    end

    Note over WEB: Configuration (independent)

    WEB-->>WEB: GET /        — HTML settings page
    WEB-->>WEB: GET /status  — JSON: ip, mode, ssid, rssi
    WEB-->>WEB: GET /config  — JSON: current config
    WEB-->>WEB: POST /save*  — persist → ESP.restart()
```

## Channels

| Channel | Protocol | Port | Direction |
|---|---|---|---|
| UI → Service | Tizen AppControl IPC | — | start / stop |
| Service → ESP32 | UDP broadcast | 9003 | discovery probe |
| ESP32 → Service | UDP unicast | 9003 | reply with IP |
| Service → ESP32 | HTTP POST | 9000 | log events |
| Service → ESP32 | UDP unicast | 9001 | RGB frames |
| Browser → ESP32 Web | HTTP | 80 | configuration |
