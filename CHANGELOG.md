# tizen-os-ambilight

## 1.2.0

### Minor Changes

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`a75ee1d`](https://github.com/exzos28/tizen-os-ambilight/commit/a75ee1d8d202ce643cca9980e68ccede06335846) Thanks [@exzos28](https://github.com/exzos28)! - The ESP32 controller can now switch the lighting off and on without losing its settings, blink the strip white to show which controller you are talking to, and restart on request. Its HTTP API also reports how many frames arrived from the TV and how long ago the last one came, and it can be called from other web pages on your network.

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`58c5e22`](https://github.com/exzos28/tizen-os-ambilight/commit/58c5e2207c40b07f5fb2667c4790d4f1a89783fa) Thanks [@exzos28](https://github.com/exzos28)! - The controller's web page is redesigned for phones and now looks like the TV app: Home has the lighting switch, brightness, mode and static colors with the live status below, and Picture, Layout and Device have the rest. A new controller opens straight on a Wi-Fi setup screen that also tells you how to find it afterwards.

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`038d682`](https://github.com/exzos28/tizen-os-ambilight/commit/038d682529086fd8bc332b1429030a82b1dd4ef6) Thanks [@exzos28](https://github.com/exzos28)! - The controller's web page now has a lighting on/off switch, a button that blinks the strip to show which controller you are on, and a Restart button, and its Status tab shows how many frames arrived from the TV and how long ago the last one came.

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`faf7f84`](https://github.com/exzos28/tizen-os-ambilight/commit/faf7f8420b9f14064922f8a3065afb389499de58) Thanks [@exzos28](https://github.com/exzos28)! - The TV app has a new Home screen: turn the lighting on and off, set the brightness and switch between following the picture and a static color right from the remote, with a live preview and the status of capture and every LED controller. When something is wrong, a banner on top says what and offers the fix, such as starting the service or searching for the controller again.

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`7f39189`](https://github.com/exzos28/tizen-os-ambilight/commit/7f39189f03b854e03e562e55fb54d58358ba49df) Thanks [@exzos28](https://github.com/exzos28)! - The TV app now has an Outputs screen and a first-run setup that turn the LED controllers and a HyperHDR server on or off and set HyperHDR's address right on the TV, with no rebuild or reinstall.

- [#7](https://github.com/exzos28/tizen-os-ambilight/pull/7) [`987df1a`](https://github.com/exzos28/tizen-os-ambilight/commit/987df1a085d4ca8a8ff146daa95a48951e331822) Thanks [@exzos28](https://github.com/exzos28)! - Every release now includes the TV package (`ambilight-tv-vX.Y.Z.wgt`, launcher and service in one file), so the TV apps can be installed with [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung) without building them. It is signed with the Tizen SDK's default certificate and has to be re-signed for your TV, which Apps2Samsung does. The TV apps now carry the project version, shown in the launcher next to its title — please include it in bug reports.

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`2db21d2`](https://github.com/exzos28/tizen-os-ambilight/commit/2db21d268ed5c1036c887eb9a0d0908bc48c1d53) Thanks [@exzos28](https://github.com/exzos28)! - The TV service can now turn its ESP32 and HyperHDR outputs on or off and change the HyperHDR target while it runs, keeping the choice on the TV instead of needing a rebuild.

- [#7](https://github.com/exzos28/tizen-os-ambilight/pull/7) [`05cd0b5`](https://github.com/exzos28/tizen-os-ambilight/commit/05cd0b5846b4f892a6fa95e8e00e9c03f882b2b9) Thanks [@exzos28](https://github.com/exzos28)! - The TV launcher now shows whether your TV is supported: a status panel reports whether screen capture works (with the hardware parameters and fps, or the reason it doesn't), whether frames are being sent to the ESP32 controllers and HyperHDR, and a live preview of the captured colors. No PC with `tools/server.py` is needed to check a TV anymore.

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`3aff9d8`](https://github.com/exzos28/tizen-os-ambilight/commit/3aff9d8049c05c4b22f998fef20cb2ff167aba74) Thanks [@exzos28](https://github.com/exzos28)! - The TV launcher's test pattern is now a set of patterns switched with ◀ ▶ — color wheel, solid colors, sides in the calibration preview colors, quadrants, a chase around the edge, a grey ramp, a hue sweep and a flicker torture test — to check the strip, its mapping and its calibration.

### Patch Changes

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`6e475ac`](https://github.com/exzos28/tizen-os-ambilight/commit/6e475ac6224314b6f504a9af87f494a1ca542130) Thanks [@exzos28](https://github.com/exzos28)! - The strip picks up the picture sooner after the TV is switched on: the service retries capture after 1, 2 and 3 s instead of 5 and 10 s.

- [#10](https://github.com/exzos28/tizen-os-ambilight/pull/10) [`836c9ec`](https://github.com/exzos28/tizen-os-ambilight/commit/836c9ecbd8f7bf954205ec9dd9090f207bf8c193) Thanks [@exzos28](https://github.com/exzos28)! - The TV service uses less memory while it runs in the background.

## 1.1.0

### Minor Changes

- [`5f4d94d`](https://github.com/exzos28/tizen-os-ambilight/commit/5f4d94de00cc68e3767cf427e3fe3b0b5a9b5c1c) Thanks [@exzos28](https://github.com/exzos28)! - The ESP32 firmware now reports its version: on the web UI's *Status* tab, in `GET /status` (`version`) and in the boot log. Please include it in bug reports.

## 1.0.0

First tagged release — see the [v1.0.0 release notes](https://github.com/exzos28/tizen-os-ambilight/releases/tag/v1.0.0).
