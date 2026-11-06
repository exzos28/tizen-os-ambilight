# HyperHDR output

The TV service can stream to a [HyperHDR](https://github.com/awawa-dev/HyperHDR) server instead of — or in addition to — this project's ESP32 firmware. HyperHDR then drives whatever LEDs it is set up for (WLED, Hyperk, Adalight, Philips Hue…), with its own smoothing and color calibration.

> [!WARNING]
> **Full-frame capture is not yet verified on an unprotected S95C video plane.** HyperHDR v22 acknowledged a bounded synthetic RGB RawImage over FlatBuffers at temporary priority 250; the test disconnected immediately and did not change server configuration. Samsung's `secvideo_api_capture_video_main` path is ABI-guarded and falls back to PPI edge samples when unavailable or when the TV rejects capture (for example, protected media returns DRM `rc=-4`).

## How it works

```
TV service ── valid NV12 frame ──► RGB RawImage (up to 960×540) ──┐
                                                                  ├─► HyperHDR FlatBuffers server (TCP 19400)
TV service ── PPI edge samples ──► configured edge image ─────────┘
                 └──────────────────────────────────────────────────► ESP32 (unchanged)
```

When available, the service converts the TV's full-frame NV12 capture directly to RGB and sends that image to HyperHDR. The existing PPI edge sampler still feeds the ESP32 and remains HyperHDR's fallback: if full-frame capture is unavailable or rejected, the service synthesizes the configured small image from the 14 edge points. The configured image width/height apply to this fallback only.

## Setup

**1. HyperHDR (v20 or newer)**

- *Network services* → *Flatbuffers server*: enabled (default), port `19400` (default).
- *LED layout*: set it up for your strip as for any video source.
- *Black border detection*: **off**. With only 14 samples, a dark top or bottom edge can look like a letterbox bar and get cropped.
- *Flatbuffers server* → *HDR tone mapping*: off (the TV already delivers SDR-range colors).
- *Smoothing*: consider raising the time from the default to ~250 ms. Frames arrive every ~140 ms, so a longer smoothing window hides the steps.
- If the TV is in a different subnet than HyperHDR, allow its address in HyperHDR's network settings — otherwise HyperHDR closes the connection right after it opens (the service log then says so).

**2. On the TV: the launcher's Outputs screen**

Open the Ambilight app on the TV, then *Outputs* in the menu (on the first start the setup
wizard asks *What lights do you have?*: pick *HyperHDR server* or *Both*). In the *HyperHDR*
card enter the host (OK opens the TV keyboard), the port (`19400`) and, if needed, the
priority, then *Apply and test*: the card shows *Connected · answering* once HyperHDR replies,
or the connection error. The switch turns the output on and off; the ESP32 card has its own
switch, so both can run at once. Changes apply at once — no rebuild or reinstall — and are
saved on the TV, where they take precedence over those keys of `res/ambilight.json` (the
image size still comes from the file) ([ARCHITECTURE.md § Outputs](ARCHITECTURE.md#outputs-1)).

**Or: the package defaults**

`tv/service/res/ambilight.json` holds the outputs a fresh install starts with (bundled into
the package, so changing it means rebuilding and reinstalling — see
[INSTALL.md § TV apps](INSTALL.md#4-tv-apps)):

```jsonc
{
  "esp32":    { "enabled": true },          // set to false if you have no ESP32 from this project
  "hyperhdr": {
    "enabled": true,
    "host": "192.168.1.50",                 // IP of the HyperHDR machine
    "port": 19400,
    "priority": 150,                        // 50–250, lower wins
    "imageWidth": 64,
    "imageHeight": 36
  }
}
```

| Key | Default | Notes |
|-----|---------|-------|
| `esp32.enabled` | `true` | Stream to ESP32 controllers found by discovery |
| `hyperhdr.enabled` | `false` | |
| `hyperhdr.host` | — | Required when enabled. An IP address is safest; a host name must be resolvable by the TV |
| `hyperhdr.port` | `19400` | HyperHDR's Flatbuffers server port |
| `hyperhdr.priority` | `150` | HyperHDR shows the source with the **lowest** number. Its own USB/system grabbers use 240/245, so 150 overrides them while the TV is streaming |
| `hyperhdr.imageWidth` / `imageHeight` | `64` / `36` | 8–256. Used for the sampled-edge fallback; full-frame capture sends its validated source dimensions |

An invalid section is disabled and reported as a `ConfigWarning` event in the service log (see [tools/server.py](../tools/README.md)). Settings sent with `set-outputs` use the same keys, ranges and defaults (without the image size, which always comes from `res/ambilight.json`), but an invalid value there rejects the whole change instead.

## Behaviour

- **Connection**: connects with a 3 s timeout, registers as `Tizen Ambilight` with the configured priority, then sends one image per captured frame.
- **Dead server detection**: HyperHDR answers every message. If no answer arrives within 3 s (e.g. the HyperHDR machine lost power), the connection is dropped and re-established.
- **Reconnect**: after 1 s, then 2 s, 4 s and every 5 s while the server stays down. Launching the service again (AppControl `command=start`, the launcher's *Start* while the service is not running) reconnects immediately.
- **Releasing the LEDs**: when the TV service stops or the connection drops, HyperHDR removes the source and falls back to its next one.
- **ESP32 in parallel**: with `esp32.enabled` the ESP32 output keeps working at the same time; neither output can slow the other down.
- **Diagnostics**: `HyperHdrConnected`, `HyperHdrDisconnected` (with the reason; repeated failures are logged about once a minute) and `HyperHdrReplyError` events go to the service log. Events from before a log host was found (including `ConfigWarning`) are queued and delivered once `tools/server.py` is discovered.

## Limitations

- **Full-plane capture is not yet tested on an unprotected video source on the S95C** — see the warning above. The existing sampled capture and HyperHDR transport were already verified separately.
- **TV standby**: what capture returns while the TV is in standby has not been measured. If the service keeps sending black frames, the TV source keeps its priority in HyperHDR until the service stops.
- **PPI edge samples are shared with ESP32**: changing their count affects the fallback image and ESP32 frame rate; it does not change full-frame resolution.
- **No on-TV settings screen yet**: the service supports changing the outputs at run time (`set-outputs`), but the launcher has no screen for it yet, so for now configuration is a file in the package.

## Third-party code

The TV service uses [Google.FlatBuffers](https://www.nuget.org/packages/Google.FlatBuffers) (Apache-2.0) to encode messages; it is bundled into the TV package. The message layout follows HyperHDR's `hyperhdr_request.fbs` (MIT).
