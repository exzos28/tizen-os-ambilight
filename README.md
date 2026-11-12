# Tizen OS Ambilight

**Ambilight for Samsung TVs, straight from the video processor.** No HDMI splitter, no camera.

[![Release](https://img.shields.io/github/v/release/exzos28/tizen-os-ambilight?style=flat-square)](https://github.com/exzos28/tizen-os-ambilight/releases/latest) [![Build](https://img.shields.io/github/actions/workflow/status/exzos28/tizen-os-ambilight/build.yml?branch=main&style=flat-square&label=build)](https://github.com/exzos28/tizen-os-ambilight/actions/workflows/build.yml) [![License](https://img.shields.io/github/license/exzos28/tizen-os-ambilight?style=flat-square)](LICENSE)

![Ambilight in action](docs/media/demo.gif)

## How it works

```mermaid
flowchart LR
  subgraph TV["Samsung TV"]
    direction TB
    VP["Video processor<br/><small>measures edge colors after decoding, DRM too</small>"]
    SVC["Service<br/><small>14 samples · ~7 fps</small>"]
    APP["TV app<br/><small>on/off · brightness · status</small>"]
    VP --> SVC
    APP <-. MessagePort .-> SVC
  end
  PHONE["Phone<br/><small>controller web app</small>"]
  ESP["ESP32<br/><small>interpolate · color-correct · smooth</small>"]
  STRIP["LED strip<br/><small>WS2811 · 12 V</small>"]
  HH["HyperHDR<br/><small>optional</small>"]
  SVC -- "UDP frames" --> ESP
  APP -- "HTTP" --> ESP
  PHONE -- "HTTP" --> ESP
  ESP --> STRIP
  SVC -. "TCP" .-> HH
```

The TV finds its controllers on its own. [Architecture](docs/ARCHITECTURE.md) · [Protocol](protocol/README.md) · [How the capture API was found](docs/RESEARCH.md)

## Screens

<p>
  <img src="docs/media/tv-home@2x.png" width="49%" alt="TV app home: lighting switch, brightness, mode, live preview of the captured colors, status">
  <img src="docs/media/tv-home-problem@2x.png" width="49%" alt="TV app when the controller stops answering: the problem and a Search now button on top">
</p>
<p>
  <img src="docs/media/web-ui-home@2x.png" width="195" alt="Controller web app on a phone: lighting switch, brightness, mode and static colors, live status">
  <img src="docs/media/web-ui-layout@2x.png" width="195" alt="Controller web app on a phone: strip layout with the sides in their preview colors, LEDs per side">
</p>

## Supported TVs

| Model | Year | Status |
|---|---|---|
| QN90D · QN92D · Q60D | 2024 | ✅ |
| Q6F | — | ❌ [#2](https://github.com/exzos28/tizen-os-ambilight/issues/2) |

[Install the app](#install-the-tv-app) — its home screen tells you whether your TV works. [Report it](docs/CONTRIBUTING.md) either way.

## Get started

```mermaid
flowchart LR
  A["1 · Build<br/><small>ESP32 + strip + PSU</small>"] --> B["2 · Flash<br/><small>Wi-Fi via the ambilight-setup hotspot</small>"]
  B --> C["3 · Calibrate<br/><small>in the controller's web app</small>"]
  C --> D["4 · Install the TV app<br/><small>Apps2Samsung, or build it</small>"]
```

[Hardware](docs/HARDWARE.md) · [Installation](docs/INSTALL.md) · [Latest release](https://github.com/exzos28/tizen-os-ambilight/releases/latest)

## Install the TV app

The quickest way is [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung): no Tizen Studio, no build.

1. Turn on Developer Mode on the TV: open **Apps**, press **1 2 3 4 5** on the remote, switch *Developer mode* on and enter the IP of the computer or phone running Apps2Samsung as *Host PC IP*. Restart the TV: hold the power button for ~5 s.
2. Open Apps2Samsung on that computer or phone (same network as the TV) and pick the TV.
3. Find **Tizen OS Ambilight** in the catalog and press *Install*. The first install asks you to sign in to Samsung once to create a certificate for your TV.
4. Open **Ambilight** on the TV and press **Start** in the banner. From then on the service starts by itself when the TV boots.

Installed it from VS Code before? Delete it on the TV first: a package signed with another certificate can't be installed over it. A release `.wgt`, TV-Store, building from source: [Installation § TV apps](docs/INSTALL.md#4-tv-apps).

## More

[Networking](docs/NETWORKING.md) · [HyperHDR](docs/HYPERHDR.md) · [TV apps](tv/README.md) · [UI kit](packages/ui/README.md) · [Tools](tools/README.md) · [Contributing](docs/CONTRIBUTING.md) · MIT [License](LICENSE)

<sub>Created for educational purposes through reverse engineering. No responsibility is assumed for any use of this code, including violations of third-party terms of service or damage to equipment. Use at your own risk.</sub>
