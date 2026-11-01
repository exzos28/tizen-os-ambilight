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
│  StatusPort.cs — answers the launcher's status requests, commands  │
│  tv/ui — launcher web app (built from tv/app: controls, status)    │
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
3. Starts the enabled outputs and, after 2 s, the capture loop (`Capture.Run()`, run and
   restarted by `CaptureLoop.cs` on its own thread). Capture does **not** wait for
   discovery — frames go to whichever outputs have somewhere to send.
4. If capture fails (library or symbols missing, invalid HW condition), retries after
   5 s, 10 s, then every 30 s.
5. AppControl `command=start` (the launcher's *Start*, shown while the service is not
   running) probes for hosts, reconnects HyperHDR and re-initialises capture (re-reading the
   hardware condition) immediately, also ending a pause; `command=stop` exits the service.
   The launcher also pauses and resumes capture over the message port ([commands](#commands)).
6. Events logged before any log host is known (config, capture init) are queued — up to
   100 — and sent once discovery finds one.
7. Opens the local message port `ambilight.status` (`StatusPort.cs`) for the launcher's
   status and commands — see [TV launcher](#2-tv-launcher-tvui).

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
failures are ignored. The `Runtime` event at startup reports the package version, the .NET
version, the GC mode (see [Memory](#memory)) and the culture. Numbers in events and in the
status are formatted culture-independently (`InvariantCulture`): Tizen sets the process
culture from the TV's region settings.

### Memory

The service starts with the TV and runs all the time, on TVs with little free RAM, so it
avoids memory it doesn't need:

- **GC**: `GCSettings.LatencyMode = Batch`, set at the start of `Main` — full collections
  block instead of running in the background, so there is no background GC thread and no
  extra memory while one runs; with a heap this small the pauses don't matter. Runtime
  options in `Service.csproj` (`InvariantGlobalization`, `ConcurrentGarbageCollection`,
  `TieredCompilation`, …) would have **no effect**: they only go into
  `Service.runtimeconfig.json`, and Tizen's `dotnet-launcher` starts CoreCLR itself with a
  fixed set of properties (often in a pre-started candidate process) without reading it.
  That's also why there is no invariant-globalization mode — ICU is the platform's.
- **Lazy loading**: the event log's `HttpClient` (and `System.Net.Http`) is created only
  once a host is known; `Google.FlatBuffers` is loaded only by `HyperHdrProtocol`, which only
  the HyperHDR output's threads call — so only when HyperHDR is enabled (a CI test checks
  that config parsing and the status JSON don't load it).
- **Per frame**: a still picture reuses the previous `EdgeFrame` instead of allocating a new
  one (a frame is never changed once handed out, since outputs may keep it); the HyperHDR
  sender reuses its image buffer and wait-handle array.

---

## 2. TV launcher (`tv/ui/`)

Preact + TypeScript web app. The source is `tv/app/`; Vite (`npm run build` there) builds it into `tv/ui`, next to
the tracked `config.xml`: one classic deferred script and one stylesheet (`assets/app.js`,
`assets/app.css`) for Chromium M94, the web engine of Tizen 7.0 (2023 TVs), the oldest
supported. `preact` is the only runtime dependency.

The TV is short on memory, so the app stays small: only the current screen is mounted, system
fonts only, no blur, shadows or transitions; the only animation is the *Test pattern*. The
widget is not preloaded (no `prelaunch.support`), and Back on Home exits it.

| Path | What |
|---|---|
| `src/App.tsx` | Shell: section rail + the one mounted screen, remote keys, status poll |
| `src/focus.ts` | Focus manager: focusable elements as rows with column positions, explicit neighbours where needed |
| `src/screens/Home.tsx` | Lighting on/off, brightness, mode + static colors, live preview, status, problem banner |
| `src/screens/TestPattern.tsx` | Full-screen rotating color wheel (96×54 canvas, scaled) to check the strip follows the picture |
| `src/screens/Placeholder.tsx` | *Picture*, *Strip layout*, *Controllers*, *Diagnostics*: not built yet |
| `src/status.ts` | Status lines, problems (most important first) and the header pill from the service status |
| `src/controllers.ts` | The discovered controllers over HTTP: `/status` + `/config` once, `/metrics` every second while Home is shown; writes |
| `src/data/` | Adapters: `service.ts` (message port), `esp32.ts` (HTTP), `mock.ts` (desktop browser, `?state=ok\|lost\|none\|off\|unsupported`), `types.ts` (wire formats) |

Remote: ◀ ▶ ▲ ▼ move, OK activates, Back exits on Home (closes the test pattern, returns
to Home from other sections). ◀ from the leftmost element moves to the rail.

### Controls

Settings live on the controllers: the launcher talks to every ESP32 the service discovered
(`status.esp32.hosts`) over its HTTP API ([NETWORKING.md](NETWORKING.md#esp32-http-api)),
form-urlencoded with a 3 s timeout. A change goes to every controller that answers.

| Control | Request |
|---|---|
| Lighting off / on | `POST /save/power power=0\|1` to every controller + service command `pause` / `resume` |
| Brightness (◀ ▶ in 5% steps, after OK) | `POST /save/leds brightness=0..255`, sent 300 ms after the last key |
| Follow the picture / Static color | `POST /save/mode led_mode=1\|0` |
| Static color swatch | `POST /save/leds color=#rrggbb` (+ `led_mode=0`) |

### Status

Once per second while the page is visible (`visibilitychange` pauses it) the launcher sends
`{request: "status"}` to the service's local message port `ambilight.status` (Tizen
MessagePort — IPC between the two apps on the TV, not a network connection), with its own
port `ambilight.ui` as the reply port. The service answers `{status: "<JSON>"}`. Commands go
the same way: `{request: "command", command: "pause" | "resume" | "rediscover"}` → `{result:
"{ok, error}"}`.

| Section | Fields | Shown as |
|---|---|---|
| `version` | Package version (`tizen-manifest.xml` / `config.xml`, synced from `package.json`), read at run time with `app_get_version`; `null` if it can't be read | — |
| `capture` | `state` (`waiting` / `starting` / `running` / `paused` / `failed`), `failure` (`library` / `symbols` / `hardware`; `null` with `failed` = unexpected exception), `error`, `failedAttempts`, `retryInMs`, `hw` (HwCondition), `frames`, `frameMs`, `lastFrameAgoMs`, `reads`, `badReads`, `lastBadRet`, `h`, `v`, `rgb` (hex, `EdgeFrame` order) | *Screen capture* line, problem banner, live preview |
| `esp32` | `enabled`, `hosts` (discovered; also `tools/server.py`), `framesSent`, `sendErrors`, `lastError` | One line per controller. *Streaming* when its `/metrics` `last_frame_ms` < 2 s; without it, *Sending* when `framesSent` grows (handed to the network, not confirmed) |
| `hyperhdr` | `enabled`, `endpoint`, `connected`, `replies`, `lastError` | *HyperHDR* line |
| `configWarnings` | `ambilight.json` problems | Problem banner |

`library` / `symbols` failures mean the TV doesn't expose the capture API; `hardware`
means `get_rgb_measure_condition` reported an unusable state (retried). A `running` capture
whose reads all fail, or with no frame for 2 s, is shown as not working. `paused` keeps the
last frame's fields; `retryInMs` is left out, since nothing is retried while paused. No
message port means the service isn't running (*Start* launches it with AppControl
`command=start`); no reply for 3 s means it doesn't respond. No controller in `hosts`, or
one that stops answering HTTP, shows *Search now* (`rediscover`).

The test pattern's hint stays in the middle of the screen, away from the edges where the
service samples.

### Commands

The same port takes commands (`ServiceCommands.cs`): `{request: "command", command: "<name>"}`,
answered to the reply port with `{result: "<JSON>"}`, where the JSON is
`{"ok": true|false, "error": null|"<why>"}`.

| `command` | Effect |
|---|---|
| `pause` | Stops capture and keeps it stopped — no retries — until `resume` (or AppControl `command=start`, or a restart of the service). Outputs stay connected; `capture.state` becomes `paused` |
| `resume` | Re-initialises capture right away, like AppControl `command=start` does for capture (also skips a pending retry delay) |
| `rediscover` | Probes for ESP32s / `tools/server.py` now instead of at the next scheduled attempt |

A missing, unknown (names are case-sensitive) or failing command answers `ok: false` with the
reason in `error`. A pause is not saved: the service starts capturing again when it restarts
(e.g. with the TV).

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

**Power gating**: with the `power` setting off the strip is dark in both modes and UDP
frames are ignored; brightness, color and mode are kept and come back when it is turned
on. What owns the strip is decided in `LightOutput.h` (host-tested), highest priority
first: identify blink → calibration preview → dark (power off) → plain / dynamic. Every
change of power or mode, and the end of a preview or identify, goes through `repaint()`,
which draws the current owner: the static color in plain mode, black otherwise (dynamic
frames then blend back in).

Frame processing (`applyAmbilight()`):

1. **Interpolate** each side's samples to the configured LED count for that side
   (`ledTop/Right/Bottom/Left`), in HSV with shortest-path hue.
2. **Color correction**: saturation boost → white balance → gamma (lookup table).
3. **Map** sides onto the physical strip using `startCorner` and `clockwise`.
4. **Smooth**: `update()` runs at ~60 Hz and blends the displayed colors toward the
   target (`SMOOTH_ALPHA = 32/255` per step).

`showCalibrationPreview()` lights the sides red / green / yellow / blue
(top / right / bottom / left) and the first LED white for 10 s. When it expires,
`update()` clears it and `repaint()`s the current output.

**Identify** (`POST /identify`, `startIdentify()`): blinks the whole strip white 3 times
(333 ms on, 333 ms off, ≈ 2 s; timeline in `LightOutput::Identify`), driven from
`update()` without blocking, at the configured brightness and power limit. It works with
the lighting off, and a preview requested meanwhile is drawn when it ends. Frames keep
updating the dynamic target during the blink. Not saved.

`UDPReceiver` drains all queued datagrams each loop and applies only the newest frame.
It counts every valid datagram (`frames_rx`, `last_frame_ms` in `GET /metrics`), applied
or not.

Limits: `MAX_LEDS = 1500`, up to 255 LEDs per side, data on GPIO 23, color order BRG
(see [HARDWARE.md](HARDWARE.md#led-color-order)).

### Web portal (`WebPortal`)

The React app in `esp32/web/` is built by `scripts/embed_web.py` (a PlatformIO pre-build
step running `npm run build`) into a single HTML file, gzipped, embedded as `src/web_ui.h`
and served from flash with `Content-Encoding: gzip` — no SPIFFS/LittleFS. It talks to the
API through `@ambilight/protocol` (`createEsp32Client`, generated types, slider ranges and
defaults from `limits`; see [protocol/README.md](../protocol/README.md#using-ambilightprotocol)).
Three tabs: Status, WiFi, LEDs. The Status tab has the lighting on/off switch
(`POST /save/power`), network and system metrics (including frames received and the age
of the last frame), and *Blink to identify* (`POST /identify`) and *Restart*
(`POST /restart`, after a confirmation) buttons. The LEDs tab has a Dynamic / Plain
switch: Dynamic shows brightness, color correction and the strip layout editor; Plain
shows LED count, brightness and the static color. Settings are saved to NVS on change; `Config::sanitize()` clamps them to
valid ranges on every load and save. Only network changes and `POST /restart` restart
the device. Every response carries `Access-Control-Allow-Origin: *` and `OPTIONS` on any
path answers the CORS preflight (see [NETWORKING.md](NETWORKING.md#esp32-http-api)).

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
| `power` | `power` | true | Lighting on/off (`POST /save/power`, `0`/`1`). Off: dark in both modes, UDP frames ignored; other settings kept |
| `gamma` | `gamma` | 2.2 | Gamma correction (UI range 0.5–3.0) |
| `saturation` | `saturation` | 1.0 | Saturation multiplier (UI range 0–2) |
| `whiteBalanceR/G/B` | `wb_r` / `wb_g` / `wb_b` | 255 | Per-channel scale |
| `preferredLastOctet` | `ip_octet` | 245 | Static IP last octet in the router's subnet; 0 = DHCP; valid 0–254 (checked against the real gateway/mask at connect) |
| `powerLimitW` | `power_limit_w` | 0 | Max estimated strip power in W; 0 = no limit (0–2000) |
| `pixelWatts` | `pixel_watts` | 0.72 | Power of one LED pixel at full white, W (0.05–5) |
