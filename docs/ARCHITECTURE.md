# Architecture Overview

DIY ambilight for Samsung Tizen TVs. Captures edge colors directly from the TV's video
processor — no HDMI splitter, no external camera.

Ports, packet formats and the HTTP API are documented in [NETWORKING.md](NETWORKING.md);
wiring in [HARDWARE.md](HARDWARE.md).

---

## System diagram

```
┌─────────────────────────────────────────────────────────────┐
│  Samsung Tizen TV                                           │
│                                                             │
│  libvideoenhance.so ─► Capture.cs ─► EdgeFrame ─┬─► Esp32Output    │
│  (HW video engine)      (.NET)                  └─► HyperHdrOutput │
│                                                                    │
│  Service_App.cs — lifecycle, outputs, event log                    │
│  Discovery.cs — finds ESP32s in the background                     │
│  tv/ui — launcher web app (Start / Stop)                           │
└──────────────────┬───────────────────────────────────┬─────────────┘
                   │ UDP 9001 (same LAN)               │ TCP 19400 (optional)
                   ▼                                   ▼
                                       HyperHDR server — see HYPERHDR.md
┌─────────────────────────────────────────────────────────────┐
│  ESP32                                                      │
│                                                             │
│  UDPReceiver  ──►  LEDController  ──►  WS2811 LED strip     │
│                                                             │
│  DiscoveryResponder  (answers TV broadcast on UDP 9003)     │
│  WebPortal  ──►  React UI (served from flash)               │
│  NetworkManager  (STA / AP captive-portal fallback)         │
│  OTAManager  (Arduino OTA)                                  │
│  RemoteLogger  (Telnet, port 2323)                          │
└─────────────────────────────────────────────────────────────┘
```

---

## 1. TV service (`tv/service/`)

.NET 6 Tizen background service (`on-boot="true"`). Entry point: `Service_App.cs`.

### Lifecycle

1. Loads the output settings from `res/ambilight.json` (bundled at build time; see
   [Configuration](#4-configuration-reference)). A missing or invalid file falls back to
   ESP32 only.
2. Starts discovery (`Discovery.cs`) in the background: broadcasts `AMBILIGHT_DISCOVER` on
   UDP 9003 and collects **all** replies for 2 s. While nobody is known it retries — 30
   quick attempts, then every 10 s; once hosts are known it re-probes every 30 s, so new
   controllers and IP changes are picked up while streaming. A host is dropped after
   2 minutes without a reply.
3. Starts the enabled outputs and, after 2 s, the capture loop (`Capture.Run()`). Capture
   does **not** wait for discovery — frames go to whichever outputs have somewhere to send.
4. If capture fails (library or symbols missing, invalid HW condition), retries after
   5 s, 10 s, then every 30 s.
5. AppControl `command=start` (the launcher's *Start*) probes for hosts, reconnects
   HyperHDR and re-initialises capture (re-reading the hardware condition) immediately;
   `command=stop` exits the service.
6. Events logged before any log host is known (config, capture init) are queued — up to
   100 — and sent once discovery finds one.

### Outputs

Capture hands every frame (`EdgeFrame`: the 14 samples) to each enabled output; no output
can block capture or another output.

| Output | Transport | Runs on | Notes |
|--------|-----------|---------|-------|
| `Esp32Output` | UDP 9001 to every discovered host | capture thread (UDP never blocks) | [Frame format](NETWORKING.md#color-frame-udp-9001) |
| `HyperHdrOutput` | TCP to HyperHDR's FlatBuffers server | own sender + reader threads, one-frame mailbox | Renders a small image (`EdgeImage.cs`); reply-based liveness, reconnect backoff 1–5 s. **Untested on a TV** — see [HYPERHDR.md](HYPERHDR.md) |

### Capture pipeline (`Capture.cs`)

The TV's video processor has a hardware PPI (Pixel Processing Interface) exposed through
`libvideoenhance.so`. It is loaded with `dlopen(RTLD_NOW)` (unresolved dependencies fail at
load time, not on first call) and a failure is logged with the loader's own message. Three
functions are called via P/Invoke:

```c
ppi_ve_get_rgb_measure_condition()   // query HW: block count, block size, screen res, delay
ppi_ve_set_rgb_measure_position()    // set where to measure (x, y in screen coords)
ppi_ve_get_rgb_measure_pixel()       // read average RGB (10-bit) of a block at that position
```

Both `ppi_ve_*` and `ve_*` symbol names are probed to handle firmware differences.

The HW has a limited number of measurement slots (2 on tested hardware). To cover all edge
points, capture batches them and pipelines the next batch's positions:

```
for each batch of N slots:
    1. get_pixel(i) → RGB      for i in [0..N-1]   (positions set in the previous step)
    2. set_position(i, x, y)   for the next batch
    3. sleep(delay)            HW needs time to average pixels
send complete frame via UDP
```

The hardware delay is 20 ms minimum; below that, results are garbage (confirmed by
diagnostics). Frame time ≈ `delay × ceil(totalPoints / slotsPerCycle)`.

**Sample points layout** — clockwise ring, each point inset `EdgeInsetPx` from the edge:

```
top:    hCount points  (left → right)
right:  vCount points  (top → bottom)
bottom: hCount points  (right → left)
left:   vCount points  (bottom → top)
```

Defaults: `hCount = 4`, `vCount = 3` → 14 points → 7 batches × 20 ms ≈ 140 ms/frame
(~7 fps). Raw values are 10-bit and are shifted to 8-bit before sending.

**Tested hardware** (Samsung 4K TV, Tizen 9.0, SDP platform):

| Parameter        | Value       |
|------------------|-------------|
| Screen           | 3840 × 2160 |
| Block size       | 16 × 16 px  |
| Slots per cycle  | 2           |
| HW delay         | 20 ms       |

### Event log

`SendLog()` posts JSON events (`CaptureStart`, `HwCondition`, `Frame`, `CaptureError`, …)
over HTTP to port 9000 of every discovered host. Only `tools/server.py` listens there;
failures are ignored.

---

## 2. TV launcher (`tv/ui/`)

Plain JavaScript Tizen web app. *Start* / *Stop* send an AppControl with
`command=start|stop` to `fCAYP0k0uD.Service`; *Debug* shows an animated color wheel
for testing the strip response. Navigation: ◀ ▶ and OK on the remote; Back exits.

---

## 3. ESP32 firmware (`esp32/src/`)

Arduino/PlatformIO project. All subsystems are singletons initialized in `setup()` and
driven in `loop()`.

### Boot sequence (`main.cpp`)

```
1. Config::begin()              load settings from NVS
2. NetworkManager               connect STA (12 s timeout); fall back to AP "ambilight-setup"
3. OTAManager::begin()          Arduino OTA
4. RemoteLogger::begin()        Telnet log server on port 2323
5. LEDController::begin()       FastLED init, static color
6. WebPortal::begin()           HTTP config UI on port 80
7. UDPReceiver::begin()         listen on UDP 9001 for frames
8. DiscoveryResponder::begin()  answer discovery probes on UDP 9003
```

**Factory reset**: hold BOOT (GPIO 0) for 3 s at any time → wipes NVS → restarts.

### Networking (`NetworkManager`)

- **STA mode**: connects via DHCP, then switches to a static IP with the last octet
  `preferredLastOctet` (setting, default 245) in the gateway's subnet, reusing the gateway
  and mask. This makes the address predictable without a DHCP reservation. If that address
  can't work on the network (outside the subnet, the router's own address, network or
  broadcast address — `StaticIp.h`), the DHCP address is kept and the reason logged.
  Nothing checks whether another device already uses it. `0` keeps the DHCP address.
- **AP mode**: opens the open network `ambilight-setup` and a captive-portal DNS that
  resolves every name to 192.168.4.1. If Wi-Fi credentials are saved (the AP is only a
  fallback, e.g. the router was still booting), it runs in `WIFI_AP_STA` mode and retries
  the station connection for 15 s every 60 s while no client is connected to the AP;
  once connected it restarts into plain STA mode.

### LED control (`LEDController`)

| Mode | `ledMode` | Behavior |
|------|-----------|----------|
| Plain | 0 | All LEDs show `colorR/G/B`; UDP frames are ignored |
| Dynamic | 1 | UDP frames drive the strip; fades to black 5 s after the last frame |

Frame processing (`applyAmbilight()`):

1. **Interpolate** each side's samples to the configured LED count for that side
   (`ledTop/Right/Bottom/Left`), in HSV with shortest-path hue.
2. **Color correction**: saturation boost → white balance → gamma (lookup table).
3. **Map** sides onto the physical strip using `startCorner` and `clockwise`.
4. **Smooth**: `update()` runs at ~60 Hz and blends the displayed colors toward the
   target (`SMOOTH_ALPHA = 32/255` per step).

`showCalibrationPreview()` lights the sides red / green / yellow / blue
(top / right / bottom / left) and the first LED white for 10 s. When it expires,
`update()` clears it and, in plain mode, restores the static color.

`UDPReceiver` drains all queued datagrams each loop and applies only the newest frame.

Limits: `MAX_LEDS = 1500`, up to 255 LEDs per side, data on GPIO 23, color order BRG
(see [HARDWARE.md](HARDWARE.md#led-color-order)).

### Web portal (`WebPortal`)

The React app in `esp32/web/` is built by `scripts/embed_web.py` (a PlatformIO pre-build
step running `npm run build`) into a single HTML file, gzipped, embedded as `src/web_ui.h`
and served from flash with `Content-Encoding: gzip` — no SPIFFS/LittleFS. Three tabs:
Status, WiFi, LEDs. The LEDs tab has a Dynamic / Plain switch: Dynamic shows brightness,
color correction and the strip layout editor; Plain shows LED count, brightness and the
static color. Settings are saved to NVS on change; `Config::sanitize()` clamps them to
valid ranges on every load and save. Only network changes restart the device.

The firmware uses the `min_spiffs.csv` partition table: two 1.875 MB OTA app slots.

### Other

- **OTA** (`OTAManager`): standard Arduino OTA; hostname and password from settings
  (default password `123456`).
- **Remote logger** (`RemoteLogger`): mirrors all log output to Serial and to one Telnet
  client on port 2323, sending whole lines.

---

## 4. Configuration reference

### TV service (`tv/service/Config.cs`) — compile-time constants

| Constant | Default | Description |
|---|---|---|
| `HttpPort` | 9000 | Event log target port |
| `UdpPort` | 9001 | Color frame target port |
| `DiscoveryPort` | 9003 | Discovery broadcast port |
| `DiscoveryTimeoutMs` | 2000 | How long to collect replies per attempt |
| `DiscoveryMaxRetries` | 30 | Quick attempts before slowing down |
| `DiscoveryRetryDelayMs` | 10000 | Interval between attempts after that (discovery never gives up) |
| `DiscoveryRefreshMs` | 30000 | Re-probe interval once hosts are known |
| `OutputConfigFile` | `ambilight.json` | Output settings file in the package's `res/` folder |
| `TargetCaptureW` | 4 | Sample points per horizontal edge |
| `TargetCaptureH` | 3 | Sample points per vertical edge |
| `EdgeInsetPx` | 250 | Distance of sample points from the screen edge, in HW pixels (3840 × 2160 on 4K) |
| `CaptureDelayMs` | 20 | Delay between `set_position` and `get_pixel`; `-1` uses the HW-reported value |

### TV output settings (`tv/service/res/ambilight.json`)

JSON (comments allowed), bundled into the package. `esp32.enabled` (default `true`) and the
`hyperhdr` section (host, port, priority, image size; disabled by default). Keys, defaults and
validation: [HYPERHDR.md](HYPERHDR.md#setup). Invalid sections are disabled and reported as
`ConfigWarning` events.

### ESP32 settings (NVS)

Stored in the `ambilight` NVS namespace, editable in the web UI (`esp32/src/Config.h`):

| Field | API name | Default | Description |
|---|---|---|---|
| `wifiSSID` / `wifiPassword` | `wifi_ssid` / `wifi_pass` | empty | Station credentials |
| `otaHostname` / `otaPassword` | `ota_host` / `ota_pass` | `ambilight` / `123456` | OTA settings |
| `numLeds` | `num_leds` | 300 | LEDs driven (set to the sum of the sides on calibration save) |
| `brightness` | `brightness` | 100 | 0–255 |
| `colorR/G/B` | `color` | white | Color in plain mode |
| `ledTop/Right/Bottom/Left` | `led_top` … | 10 / 6 / 10 / 6 | LEDs per side |
| `startCorner` | `start_corner` | 0 | 0 = top-left, 1 = top-right, 2 = bottom-right, 3 = bottom-left |
| `clockwise` | `clockwise` | true | Strip direction from the start corner |
| `ledMode` | `led_mode` | 1 | 0 = plain, 1 = dynamic |
| `gamma` | `gamma` | 2.2 | Gamma correction (UI range 0.5–3.0) |
| `saturation` | `saturation` | 1.0 | Saturation multiplier (UI range 0–2) |
| `whiteBalanceR/G/B` | `wb_r` / `wb_g` / `wb_b` | 255 | Per-channel scale |
| `preferredLastOctet` | `ip_octet` | 245 | Static IP last octet in the router's subnet; 0 = DHCP; valid 0–254 (checked against the real gateway/mask at connect) |
| `powerLimitW` | `power_limit_w` | 0 | Max estimated strip power in W; 0 = no limit (0–2000) |
| `pixelWatts` | `pixel_watts` | 0.72 | Power of one LED pixel at full white, W (0.05–5) |
