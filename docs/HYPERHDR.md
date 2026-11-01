# HyperHDR output

The TV service can stream to a [HyperHDR](https://github.com/awawa-dev/HyperHDR) server instead of — or in addition to — this project's ESP32 firmware. HyperHDR then drives whatever LEDs it is set up for (WLED, Hyperk, Adalight, Philips Hue…), with its own smoothing and color calibration.

> [!WARNING]
> **Untested on a real TV.** The HyperHDR output has been tested only in CI: against a real HyperHDR v22 server (streaming, registration, the server freezing, crashing and restarting), with the TV capture replaced by synthetic frames. It has **not** been run on a Samsung TV yet — please report how it works for you in [#1](https://github.com/exzos28/tizen-os-ambilight/issues/1).

## How it works

```
TV service ── 14 edge samples ──► 64×36 image ──► HyperHDR FlatBuffers server (TCP 19400)
                                                     │
                                                     └─► your LED device(s)
```

The TV measures 14 points along the screen edges (~7 updates/s). For HyperHDR the service turns them into a small image: every pixel takes the color of the nearest screen edge, interpolated along that edge — the same interpolation the ESP32 firmware uses. HyperHDR treats it like any other video source and maps it to LEDs with its own LED layout.

## Setup

**1. HyperHDR (v20 or newer)**

- *Network services* → *Flatbuffers server*: enabled (default), port `19400` (default).
- *LED layout*: set it up for your strip as for any video source.
- *Black border detection*: **off**. With only 14 samples, a dark top or bottom edge can look like a letterbox bar and get cropped.
- *Flatbuffers server* → *HDR tone mapping*: off (the TV already delivers SDR-range colors).
- *Smoothing*: consider raising the time from the default to ~250 ms. Frames arrive every ~140 ms, so a longer smoothing window hides the steps.
- If the TV is in a different subnet than HyperHDR, allow its address in HyperHDR's network settings — otherwise HyperHDR closes the connection right after it opens (the service log then says so).

**2. TV service configuration**

Edit `tv/service/res/ambilight.json` **before building** the TV package (it is bundled into the package):

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

Then build and install as described in [INSTALL.md § TV apps](INSTALL.md#4-tv-apps). Changing the settings later means editing the file, rebuilding and reinstalling.

| Key | Default | Notes |
|-----|---------|-------|
| `esp32.enabled` | `true` | Stream to ESP32 controllers found by discovery |
| `hyperhdr.enabled` | `false` | |
| `hyperhdr.host` | — | Required when enabled. An IP address is safest; a host name must be resolvable by the TV |
| `hyperhdr.port` | `19400` | HyperHDR's Flatbuffers server port |
| `hyperhdr.priority` | `150` | HyperHDR shows the source with the **lowest** number. Its own USB/system grabbers use 240/245, so 150 overrides them while the TV is streaming |
| `hyperhdr.imageWidth` / `imageHeight` | `64` / `36` | 8–256. Larger doesn't add detail (there are only 14 samples), only makes each LED area smoother |

An invalid section is disabled and reported as a `ConfigWarning` event in the service log (see [tools/server.py](../tools/README.md)).

## Behaviour

- **Connection**: connects with a 3 s timeout, registers as `Tizen Ambilight` with the configured priority, then sends one image per captured frame.
- **Dead server detection**: HyperHDR answers every message. If no answer arrives within 3 s (e.g. the HyperHDR machine lost power), the connection is dropped and re-established.
- **Reconnect**: after 1 s, then 2 s, 4 s and every 5 s while the server stays down. Launching the service again (AppControl `command=start`, the launcher's *Start* while the service is not running) reconnects immediately.
- **Releasing the LEDs**: when the TV service stops or the connection drops, HyperHDR removes the source and falls back to its next one.
- **ESP32 in parallel**: with `esp32.enabled` the ESP32 output keeps working at the same time; neither output can slow the other down.
- **Diagnostics**: `HyperHdrConnected`, `HyperHdrDisconnected` (with the reason; repeated failures are logged about once a minute) and `HyperHdrReplyError` events go to the service log. Events from before a log host was found (including `ConfigWarning`) are queued and delivered once `tools/server.py` is discovered.

## Limitations

- **Untested on a TV** — see the warning above.
- **TV standby**: what capture returns while the TV is in standby has not been measured. If the service keeps sending black frames, the TV source keeps its priority in HyperHDR until the service stops.
- **Same samples for every output**: the number of sample points is shared by all outputs; raising it for HyperHDR also lowers the ESP32 frame rate.
- **No on-TV settings screen**: configuration is a file in the package.

## Third-party code

The TV service uses [Google.FlatBuffers](https://www.nuget.org/packages/Google.FlatBuffers) (Apache-2.0) to encode messages; it is bundled into the TV package. The message layout follows HyperHDR's `hyperhdr_request.fbs` (MIT).
