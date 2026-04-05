# TV Service

.NET Tizen service that captures screen edge colors and sends them to a server via UDP.

## How it works

Samsung TVs have a hardware **PPI (Pixel Processing Interface)** inside the video enhancement engine. The native library `libvideoenhance.so` exposes three functions that allow reading average RGB values at arbitrary screen positions:

```c
// Query HW capabilities (block count, block size, screen resolution, delay)
int ppi_ve_get_rgb_measure_condition(VEPPIRgbMeasureInfo_t *info);

// Set where to measure (x, y in screen coordinates)
int ppi_ve_set_rgb_measure_position(int idx, int x, int y);

// Read the average RGB for the 16x16 block at that position
int ppi_ve_get_rgb_measure_pixel(int idx, VEPPIRgbMeasure_t *rgb);
```

This is the same mechanism that Samsung's **Hue Sync** app uses internally — discovered by decompiling `Tizen.TV.System.ContentAnalysis.dll`, the .NET wrapper Samsung provides to partner apps. We bypass the managed wrapper and call `libvideoenhance.so` directly via dlopen/P/Invoke.

### Capture loop

The hardware has a limited number of measurement slots (2 on our TV). To sample all edge points, the service iterates through them in batches:

```
for each batch of 2 points:
    set_position(0, x0, y0)    -- tell HW slot 0 where to measure
    set_position(1, x1, y1)    -- tell HW slot 1 where to measure
    sleep(delay)               -- wait for HW to measure
    read_pixel(0) -> RGB       -- read result from slot 0
    read_pixel(1) -> RGB       -- read result from slot 1

send all colors via UDP
```

### Performance

The bottleneck is the hardware measurement delay. After setting a position, the video processor needs time to average the 16x16 pixel block. The HW requires **20ms** — this is the minimum reliable delay we use.

| Delay | Points | Cycles | Frame time | FPS |
|-------|--------|--------|------------|-----|
| 20ms  | 50     | 25     | 500ms      | ~2  |

Tune `CaptureDelayMs`, `TargetCaptureW`, `TargetCaptureH`, and `TargetFps` in `Config.cs`.

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
  Service_App.cs      App lifecycle, server discovery, HTTP logging
  Capture.cs          HW PPI capture loop via libvideoenhance.so
  Config.cs           Ports, sample point count, delay, FPS limit
  tizen-manifest.xml  Manifest with contentanalysis privilege

capture-tool/         Standalone test tool (runs via corerun without installing)
```

## Privilege

The manifest includes `http://developer.samsung.com/privilege/contentanalysis` (Samsung partner level). A Samsung partner certificate is required to sign the package.
