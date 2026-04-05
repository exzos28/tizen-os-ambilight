# Architecture Overview

DIY ambilight for Samsung Tizen TVs. Captures edge colors directly from the TV's video
processor — no HDMI splitter, no external camera.

---

## System diagram

```
┌─────────────────────────────────────────────────────────────┐
│  Samsung Tizen TV                                           │
│                                                             │
│  libvideoenhance.so  ──►  Capture.cs  ──► UDP packet       │
│  (HW video engine)         (.NET)          port 9001        │
│                                                             │
│  Service_App.cs — lifecycle, discovery, HTTP logging        │
└───────────────────────────────────┬─────────────────────────┘
                                    │ UDP  (same LAN)
                                    ▼
┌─────────────────────────────────────────────────────────────┐
│  ESP32                                                      │
│                                                             │
│  UDPReceiver  ──►  LEDController  ──►  WS2811 LED strip    │
│                                                             │
│  WebPortal  ──►  React UI (served from flash)              │
│  NetworkManager  (STA / AP captive-portal fallback)         │
│  OTAManager  (Arduino OTA)                                  │
│  RemoteLogger  (Telnet)                                     │
└─────────────────────────────────────────────────────────────┘
```

Discovery (startup only): TV broadcasts `AMBILIGHT_DISCOVER` on UDP:9003 → ESP32 replies
with its IP → TV learns where to send frames.

---

## Components

### 1. TV service (`tv/service/`)

.NET 6 Tizen background service. Entry point: `Service_App.cs`.

**Capture pipeline** (`Capture.cs`):

The TV's video processor has a hardware PPI (Pixel Processing Interface) exposed through
`libvideoenhance.so`. Three functions are accessed via `dlopen` / P/Invoke:

```c
ppi_ve_get_rgb_measure_condition()   // query HW: block count, block size, screen res, delay
ppi_ve_set_rgb_measure_position()    // set where to measure (x, y in screen coords)
ppi_ve_get_rgb_measure_pixel()       // read average RGB of a 16×16 block at that position
```

The HW has a limited number of measurement slots (2 on tested hardware). To cover all edge
points, capture batches them:

```
for each batch of N slots:
    1. set_position(i, x, y)   for i in [0..N-1]
    2. sleep(delay)            HW needs time to average pixels
    3. get_pixel(i) → RGB      for i in [0..N-1]
    advance to next batch positions
send complete frame via UDP
```

The hardware delay is 20 ms minimum. Below that, results are garbage (confirmed by
diagnostics). The final FPS depends on: `delay × ceil(totalPoints / slotsPerCycle)`.

**Sample points layout** — edge pixels are sampled in a clockwise ring:

```
top:    hCount points  (left → right, top edge)
right:  vCount points  (top → bottom, right edge)
bottom: hCount points  (right → left, bottom edge)
left:   vCount points  (bottom → top, left edge)
```

Defaults: `hCount=6`, `vCount=4` → 20 points → 10 batches × 20 ms = 200 ms/frame (~5 fps).

**Tested hardware** (Samsung 4K TV, Tizen 9.0, SDP platform):

| Parameter        | Value       |
|------------------|-------------|
| Screen           | 3840 × 2160 |
| Block size       | 16 × 16 px  |
| Slots per cycle  | 2           |
| HW delay         | 20 ms       |

**Discovery** (`Service_App.cs → DiscoverServer()`): at startup, broadcasts
`AMBILIGHT_DISCOVER` on UDP:9003 up to 30 times with 2-second timeouts. Expects reply
`AMBILIGHT_DISCOVER:<ip>`. If no reply, service exits.

**Logging**: HTTP POST JSON events to `http://<esp32>:9000`. Non-critical — failures are
silently swallowed.

---

### 2. ESP32 firmware (`esp32/src/`)

Arduino/PlatformIO project. All major subsystems are singletons initialized in `setup()`
and driven in `loop()`.

**Boot sequence** (`main.cpp`):

```
1. Config::begin()          load NVS (WiFi creds, LED layout, calibration)
2. NetworkManager           connect STA; fall back to AP "ambilight-setup" if fail
3. OTAManager::begin()      Arduino OTA (STA mode only)
4. RemoteLogger::begin()    Telnet log server
5. LEDController::begin()   FastLED init, static color
6. WebPortal::begin()       HTTP config UI
7. UDPReceiver::begin()     listen on UDP:9001 for ambilight frames
```

**Factory reset**: hold BOOT button (GPIO 0) for 3 seconds at any time → wipes NVS →
restarts.

**Networking** (`NetworkManager`):

- STA mode: tries DHCP, then attempts a semi-static IP by requesting the preferred last
  octet (`PREFERRED_IP_LAST_OCTET = 245`) from the gateway. This makes the IP predictable
  without a reserved DHCP lease.
- AP mode: opens `ambilight-setup` (open), starts a captive-portal DNS that redirects all
  DNS queries to 192.168.4.1 so the config page opens automatically on most devices.

**LED control** (`LEDController`):

Two modes controlled by `Config::ledMode`:

| Mode | Value | Behavior |
|------|-------|----------|
| Static color | 0 | Fills all LEDs with `colorR/G/B` |
| Ambilight | 1 | Applies incoming UDP frames; ignores frames in mode 0 |

`applyAmbilight()` receives a frame, interpolates each side's sample points to the actual
LED count for that side, then maps the sides onto the physical strip using `startCorner`
and `clockwise` config.

`showCalibrationPreview()` lights each side in a distinct color and marks LED 0 white —
lets users visually verify strip direction before going live.

**UDP receiver** (`UDPReceiver`): listens on port 9001. Packet format:

```
[hCount : 1 byte]
[vCount : 1 byte]
[top    : hCount × 3 bytes RGB]
[right  : vCount × 3 bytes RGB]
[bottom : hCount × 3 bytes RGB]
[left   : vCount × 3 bytes RGB]
```

Max packet for default config (h=6, v=4): 2 + 20×3 = 62 bytes. Buffer is capped at 512
bytes for ESP32 safety.

**Web portal** (`WebPortal` + React UI built into `web_ui.h`): serves a single-page app
with four tabs — Status, LEDs, Calibration, WiFi. All config changes are persisted to NVS
immediately via `Config::save()`.

**OTA** (`OTAManager`): standard Arduino OTA. Hostname and password from config.

**Remote logger** (`RemoteLogger`): Telnet server on port 23. Forwards all `Serial`
output so you can monitor the device without USB.

---

### 3. Configuration

**TV side** (`tv/service/Config.cs`) — compile-time constants:

| Constant | Default | Description |
|---|---|---|
| `UdpPort` | 9001 | Target port for color frames |
| `DiscoveryPort` | 9003 | UDP broadcast port |
| `DiscoveryMaxRetries` | 30 | Attempts before giving up |
| `TargetCaptureW` | 6 | Sample points per horizontal edge |
| `TargetCaptureH` | 4 | Sample points per vertical edge |
| `CaptureDelayMs` | 20 | ms between set_position and get_pixel |
| `TargetFps` | 60 | Frame rate cap (actual FPS is HW-limited) |

**ESP32 side** (`esp32/src/Config.h`) — persisted in NVS, editable via web UI:

| Field | Description |
|---|---|
| `wifiSSID / wifiPassword` | Station credentials |
| `otaHostname / otaPassword` | OTA update settings |
| `numLeds` | Total LED count in strip |
| `brightness` | 0–255 |
| `colorR/G/B` | Static color (mode 0) |
| `ledTop/Right/Bottom/Left` | LEDs per side (calibration) |
| `startCorner` | 0=top-left, 1=top-right, 2=bottom-right, 3=bottom-left |
| `clockwise` | Strip winding direction |
| `ledMode` | 0=static, 1=ambilight |

---

## Network ports

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| 9000 | HTTP | TV → ESP32 | Log events (JSON POST) |
| 9001 | UDP | TV → ESP32 | Color frames |
| 9003 | UDP broadcast | TV → LAN | ESP32 discovery |
| 23 | TCP | Client → ESP32 | Telnet remote log |
| 80 | HTTP | Browser → ESP32 | Web config portal |
| OTA port | TCP | Host → ESP32 | Arduino OTA |

---

## Hardware

```
12V PSU
  └─► DC-DC 12V→5V ─► ESP32 (GPIO 23 → 330Ω → WS2811 DATA)
  └─► WS2811 12V LED strip (via JST-SM 3-pin: +12V / GND / DATA)
```

Decoupling: 1000 µF / 16 V on the 12 V rail, 100 µF / 10 V after the DC-DC converter.

LED pin: GPIO 23. Max addressable: 1500 LEDs (`MAX_LEDS` in `LEDController.h`).

---

## Key implementation notes

- `libvideoenhance.so` is probed for two symbol name variants (`ppi_ve_*` and `ve_*`) to
  handle differences between Tizen firmware versions.
- The TV service has no retry logic after `Capture.Run()` fails — if `dlopen` or symbol
  lookup fails, the service exits silently (logged via HTTP first if possible).
- ESP32 ignores UDP frames when `ledMode != 1` — the ambilight receiver is always
  listening, just not acting.
- The React web UI is compiled to a single `web_ui.h` header and served from ESP32 flash;
  there is no SD card or SPIFFS dependency.
