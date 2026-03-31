# Screen Capture Research — Samsung Tizen TV

## Goal
Obtain approximate edge colors from the screen (for ambilight) programmatically, from inside the TV, without external devices. No full image needed.

## Display architecture
- Platform: Samsung SDP (not Exynos), Tizen 9.0
- DRM: 3 cards (`/dev/dri/card0..2`), single connector `card0-LVDS-1` (internal panel)
- No writeback connector, no `/dev/fb*`
- V4L2: MFC decoders (video10-14), HDMI inputs (video20-22,24), scaler/encoder (video30-33,40), JPEG (video50), tuner (video60-63)
- Video layer in secure memory (TrustZone TZASC)

## Working solution: libvideoenhance.so (HW PPI)

### How we found it
Decompiled Hue Sync (.NET DLLs: HueSyncService.dll, HueSync.dll) and discovered:
- Hue Sync uses the `Tizen.TV.System.ContentAnalysis` API
- This API wraps `libvideoenhance.so` (HW capture) and `libcapi-video-capture.so` (SW capture)
- HW capture uses PPI (Pixel Processing Interface) — direct hardware access to the video processor

### API (3 functions in libvideoenhance.so)

```c
// 1. Query HW capabilities
int ppi_ve_get_rgb_measure_condition(VEPPIRgbMeasureInfo_t *info);
// Returns: measureBlockCnt (blocks per cycle), measureBlockWidth/Height,
//          measureFullWidth/Height (3840x2160), measureDelay (20ms)

// 2. Set measurement position
int ppi_ve_set_rgb_measure_position(int idx, int x, int y);

// 3. Read average RGB at position
int ppi_ve_get_rgb_measure_pixel(int idx, VEPPIRgbMeasure_t *rgb);
// Returns: mean_R, mean_G, mean_B (0-255)
```

Alternative symbol names: `ve_get_rgb_measure_condition`, `ve_set_rgb_measure_position`, `ve_get_rgb_measure_pixel`.

### Data structures

```c
struct VEPPIRgbMeasureInfo_t {
    int measureBlockCnt;    // blocks per cycle (2 on our TV)
    int measureBlockWidth;  // block width (16)
    int measureBlockHeight; // block height (16)
    int measureDelay;       // delay between set and get (20ms)
    int measureFullWidth;   // full screen width (3840)
    int measureFullHeight;  // full screen height (2160)
};

struct VEPPIRgbMeasure_t {
    int mean_R, mean_G, mean_B; // average RGB within the block
    int lowInputLagOnOff;
};
```

### Architecture

```
[libvideoenhance.so] — hardware PPI in the video processor
        | dlopen + P/Invoke
[Our service (Capture.cs)] — .NET Tizen service
  - Places 50 sample points along edges (16 top + 9 right + 16 bottom + 9 left)
  - Reads 2 points per cycle (blocksPerCycle=2), 20ms per cycle
  - 25 cycles x 20ms = 500ms per frame ~ 2 FPS
  - Sends RGB data via UDP
        | UDP
[server.py] — desktop GUI ambilight visualization
```

### Measured TV parameters
- Resolution: 3840x2160 (4K)
- Measurement block: 16x16 pixels
- Blocks per cycle: 2
- Delay: 20ms
- Privilege: `http://developer.samsung.com/privilege/contentanalysis` (Samsung partner)

## Hue Sync internals (decompiled)

### Hue Sync Service (package: 2hy8Y93qcx)
- Path: `/opt/usr/apps/2hy8Y93qcx/`
- Type: .NET Tizen service-application, on-boot=true
- DLLs: `HueSyncService.dll`, `EDK.dll` (Hue Entertainment SDK), `Common.dll`, `ClipV2.dll`, `HueStreamClient.dll`
- Uses `Tizen.TV.System.ContentAnalysis` for capture (not DataControl as initially assumed)
- DataControl is used only for state/settings exchange with tvsensor-iot-service

Key privileges:
- `http://developer.samsung.com/privilege/contentanalysis` — Samsung partner API
- `http://developer.samsung.com/privilege/contentsinfo`

ContentAnalysis wrapper location: `/usr/share/dotnet.tizen/framework/Tizen.TV.System.ContentAnalysis.dll`

### tvsensor-iot-service (Samsung native service)
- Path: `/opt/usr/apps/com.samsung.tv.tvsensor-iot-service/bin/tvsensor-iot-service`
- Type: native C++ binary, runs as `owner`
- Captures via MDE Framework, forwards to Hue Sync via DataControl

Dependencies:
- `libcapi-mde-framework-tv.so` — Media Display Engine Framework
- `libcapi-screen.so.0` — Screen API
- `libcapi-media-camera.so.0` — Camera API
- `libdata-control.so.0` — DataControl (IPC with Hue Sync)

### MDE Framework
```
[mde-framework] PID 3693
  - Central IPC hub (pub/sub via MDEF protocol)
  - Plugins: /usr/lib/mde-framework/libmde-service-*.so
  - Services: camera.videocapture, camera.imagecapture, etc.
```

## Rejected approaches

### MDEF Protocol Client
`libcapi-mde-framework-tv.so` — protocol_create returns handle=0, subscribe returns error 3.
Library contains mdef_service_* (for providers) but no notification callback for clients.

### DataControl (impersonating Hue Sync)
Requires app ID `com.lighting.HueSyncService` + package `2hy8Y93qcx`. Author certificate mismatch blocks installation. tvsensor-iot-service hardcodes the Hue Sync app ID for data_control_map_set.

### DRM/KMS planes
Secure video plane is inaccessible — TrustZone hardware protection. Non-secure planes only contain the UI overlay.

### HDMI splitter
No external source — content comes from built-in TV apps.

### screen-analysis ecosystem (AI/CV)
Services like `screen-analysis-service-manager`, `subtitle-recog-service` etc. are AI consumers — they receive images, not capture them.

### CVE-2025-4632
Path traversal in Samsung MagicINFO 9 Server (digital signage). Unrelated to Tizen OS.

## TV internals

```
/dev/dri/card0, card1, card2
/dev/video10-14    — sdp-mfc-dec
/dev/video20-22,24 — sdp-hdmi, inputs
/dev/video28       — hdmi_switch
/dev/video30-33    — sdp-hen-enc
/dev/video40       — unknown
/dev/video50-51    — sdp-jpeg, sdp-dvde-dec
/dev/video60-63    — TZTV TUNER

D-Bus (system):
  screend.message, screend.powercontrol
  com.samsung.TVService.Signal.tvs1.*

vconf:
  memory/screen_analysis/state = 0
  memory/tdm_video/playback_info_scaler_0..3
```
