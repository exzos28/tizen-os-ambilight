# TV apps

| Path | What |
|------|------|
| `service/` | .NET Tizen service that captures screen edge colors and streams them to ESP32 controllers (UDP) and, optionally, a HyperHDR server (TCP) |
| `ui/` | Launcher web app with *Start* / *Stop* / *Debug* buttons and a status panel that shows whether the TV supports capture and where the colors go |

Both apps ship as one hybrid package — building `ui/` also builds `service/`. Build, signing and installation: [docs/INSTALL.md § TV apps](../docs/INSTALL.md#4-tv-apps).

Releases attach the package as `ambilight-tv-vX.Y.Z.wgt`, built in CI by `scripts/make_package.sh` (`.github/workflows/tv-package.yml`): the service from `service/Service.csproj` with the Tizen .NET workload (as in VS Code), the hybrid `.wgt` with the Tizen-Core CLI (`tz pack`, as the VS Code extension), signed with the Tizen SDK's default certificate for Apps2Samsung to re-sign.

## How it works

Samsung TVs have a hardware **PPI (Pixel Processing Interface)** inside the video enhancement engine. The native library `libvideoenhance.so` exposes three functions that allow reading average RGB values at arbitrary screen positions:

```c
// Query HW capabilities (block count, block size, screen resolution, delay)
int ppi_ve_get_rgb_measure_condition(VEPPIRgbMeasureInfo_t *info);

// Set where to measure (x, y in screen coordinates)
int ppi_ve_set_rgb_measure_position(int idx, int x, int y);

// Read the average 10-bit RGB for the 16x16 block at that position
int ppi_ve_get_rgb_measure_pixel(int idx, VEPPIRgbMeasure_t *rgb);
```

This is the same mechanism that Samsung's **Hue Sync** app uses internally — discovered by decompiling `Tizen.TV.System.ContentAnalysis.dll`, the .NET wrapper Samsung provides to partner apps. We bypass the managed wrapper and call `libvideoenhance.so` directly via dlopen/P/Invoke. The full story is in [docs/RESEARCH.md](../docs/RESEARCH.md).

### Capture loop

The hardware has a limited number of measurement slots (2 on tested TVs). To sample all edge points, the service iterates through them in batches, setting the next batch's positions while the current results are read:

```
set positions for batch 0, sleep(delay)
loop:
    for each batch:
        read_pixel(0..N-1) -> RGB         -- results for the current batch
        set_position(0..N-1, next batch)  -- start measuring the next one
        sleep(delay)                      -- wait for HW to measure
    hand the frame to the outputs (ESP32 UDP, HyperHDR)
```

### Performance

The bottleneck is the hardware measurement delay: after setting a position, the video processor needs **20 ms** to average the block. Shorter delays return garbage.

| Delay | Points | Batches (2 slots) | Frame time | FPS |
|-------|--------|-------------------|------------|-----|
| 20 ms | 14 (current: 4 + 3 + 4 + 3) | 7 | ~140 ms | ~7 |
| 20 ms | 50 (first prototype) | 25 | ~500 ms | ~2 |

More points give finer spatial detail at a lower frame rate. Since the ESP32 interpolates between points and smooths transitions, a few points per edge are usually enough.

### Tuning

Sample point counts, edge inset and capture delay are compile-time constants in `service/Config.cs`; their defaults and effects are listed in [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md#tv-service-tvserviceconfigcs--compile-time-constants). A larger `EdgeInsetPx` helps skip letterbox bars (~270 px each for 2.39:1 content on a 4K panel).

### Measured HW parameters (Samsung 4K TV, Tizen 9.0, SDP)

| Parameter       | Value     |
|-----------------|-----------|
| Screen          | 3840x2160 |
| Block size      | 16x16 px  |
| Blocks per cycle| 2         |
| HW delay        | 20ms      |

## Files

```
service/
  Service_App.cs      App lifecycle, outputs, HTTP event log
  Capture.cs          HW PPI capture loop via libvideoenhance.so → EdgeFrame
  CaptureLoop.cs      Runs capture on its own thread: retries, pause / resume, re-init
  Discovery.cs        Background UDP discovery of ESP32s / tools/server.py
  ServiceStatus.cs    Capture state + JSON for the launcher's status panel
  ServiceCommands.cs  Launcher commands: pause, resume, rediscover
  StatusPort.cs       Local message port the launcher polls for status and sends commands to
  Esp32Output.cs      UDP frames to discovered ESP32s
  HyperHdrOutput.cs   HyperHDR FlatBuffers client (untested on a TV, see docs/HYPERHDR.md)
  HyperHdrProtocol.cs FlatBuffers messages, written by hand against HyperHDR's schema
  EdgeImage.cs        Edge samples → small RGB image for HyperHDR
  OutputConfig.cs     Parses res/ambilight.json
  EdgeFrame.cs        One frame of edge samples
  Config.cs           Ports, discovery timing, sample points, edge inset, delay
  res/ambilight.json  Output settings — edit before building
  tizen-manifest.xml  Manifest: on-boot service, contentanalysis privilege

ui/
  index.html, main.js Launcher: Start / Stop the service, status panel, Debug color wheel
  config.xml          Web app manifest (application.launch privilege)
  tizen_web_project.yaml  Declares ../service as a dependency → hybrid package
```

## Privilege

The service manifest includes `http://developer.samsung.com/privilege/contentanalysis`, which Samsung lists as a partner privilege. In Developer Mode a regular **Public** Samsung distributor certificate bound to the TV's DUID is enough — see [docs/INSTALL.md](../docs/INSTALL.md#42-connect-and-create-a-certificate).
