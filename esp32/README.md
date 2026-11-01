# ESP32 firmware

PlatformIO / Arduino firmware that receives color frames from the TV and drives a WS2811 strip. Includes the phone web app for setup and control (Preact, on the shared UI kit `packages/ui`), embedded into the firmware binary.

Setup and flashing: [docs/INSTALL.md § ESP32](../docs/INSTALL.md#1-esp32-firmware). Internals: [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md#3-esp32-firmware-esp32src).

## Layout

```
platformio.ini            Build config: env esp32dev (USB upload), env ota (Wi-Fi upload)
scripts/embed_web.py      Pre-build step: npm run build in web/ → gzipped byte array in src/web_ui.h
src/
  main.cpp                setup()/loop(), factory reset on BOOT button
  Config.*                Settings persisted in NVS
  NetworkManager.*        Wi-Fi STA, AP + captive-portal fallback, preferred IP
  UDPReceiver.*           Color frames on UDP 9001
  DiscoveryResponder.*    Answers TV discovery on UDP 9003
  LEDController.*         Interpolation, color correction, strip mapping, smoothing, power limit
  StripLayout.h           Strip traversal + interpolation math (no Arduino deps, unit-tested)
  PowerLimit.h            Power estimate + brightness scaling (no Arduino deps, unit-tested)
  StaticIp.h              Static-address validation against the DHCP subnet (unit-tested)
  LightOutput.h           Power gating, output priority, identify blink timeline (unit-tested)
  WebPortal.*             HTTP server: web UI and JSON API
  OTAManager.*            Arduino OTA
  RemoteLogger.*          Log to Serial + Telnet (port 2323)
web/                      Preact + Vite phone web app (built into one HTML file, ~23 KB gzipped):
                          Wi-Fi setup first in AP mode, then Home (lighting, brightness, mode,
                          live status), Picture, Layout, Device; API client, types and limits
                          from packages/protocol, look from packages/ui (@ambilight/ui)
test/                     Host unit tests (pio test -e native)
```

## Commands

```sh
pio run                   # build (also builds the web app; needs npm install in web/ once)
pio test -e native        # host unit tests (StripLayout.h, PowerLimit.h, StaticIp.h, LightOutput.h)
pio run -t upload         # flash over USB
pio run -e ota -t upload  # flash over Wi-Fi (ambilight.local, see [env:ota] in platformio.ini)
pio device monitor        # serial log, 115200 baud
scripts/make_release.sh   # build + factory/app images into release/ (what GitHub Releases publish)
nc <esp32-ip> 2323        # remote log over Telnet
```

`src/web_ui.h` is generated on every build and is not committed.

**Web app without a controller:** `npm run dev` in `web/` and open it with `?mock=ok|ap|noframes|limited|offline` (see [CONTRIBUTING.md](../docs/CONTRIBUTING.md#developing-the-web-ui)); `npm run build:mock` writes the same page with the mock to `web/dist-mock/`. The firmware build never contains the mock.

**Releases** are driven by [changesets](../.changeset/README.md): merging the *chore: release* PR tags `vX.Y.Z`, creates the GitHub release from `CHANGELOG.md` and runs `.github/workflows/release.yml`, which builds the images with `scripts/make_release.sh` and attaches them. Pushing a `v*` tag by hand also works. Every CI run uploads the images as the `esp32-firmware` artifact.

## Compile-time constants

| Constant | File | See |
|----------|------|-----|
| `LED_PIN`, `MAX_LEDS` | `src/LEDController.h` | [HARDWARE.md](../docs/HARDWARE.md#notes) |
| Color order (`addLeds<WS2811, LED_PIN, BRG>`) | `src/LEDController.cpp` | [HARDWARE.md](../docs/HARDWARE.md#led-color-order) |
| `UDP_PORT` | `src/UDPReceiver.h` | [NETWORKING.md](../docs/NETWORKING.md#ports) |
