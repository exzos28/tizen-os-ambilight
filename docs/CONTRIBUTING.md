# Contributing

Thanks for helping out! The most valuable contribution right now is **testing on more TV models** — whether capture works depends on the TV's processor, and we only know about the models listed in the README.

## Reporting a TV model

Whether it works or not, both results are useful. Open a [TV compatibility report](https://github.com/exzos28/tizen-os-ambilight/issues/new?template=tv-compatibility.yml) or a pull request that adds a row to the *Supported TVs* table in [README.md](../README.md).

Please include:

| Field | Where to find it |
|-------|------------------|
| Model code | Settings → Support → About This TV (e.g. `QE65QN90DAU`) |
| Model year and Tizen version | If you know it — the Tizen version can change with firmware updates |
| Software version | Settings → Support → About This TV → Software version |
| Result | Works / doesn't work, and what you tested (streaming app, HDMI input, broadcast) |
| Service log | See below |

### Checking your TV

Install the TV apps and open *Ambilight*: Home says whether screen capture works on this TV
(a problem banner with the reason if it doesn't) and whether the colors reach the LED
controllers or HyperHDR. *Test pattern* fills the screen with a rotating color wheel: the strip
should follow it, and the *Live preview* on Home shows the colors the TV captured.

*Diagnostics* (in the menu on the left) has the details for a report: the verdict, the model,
what the TV's video processor reports (the same values as the service's `HwCondition` log
line), frame time and failed reads, and what the LED controllers and HyperHDR report back.
Its *Report this TV* QR code opens a TV compatibility report on your phone with the model, the
result and those values already filled in; add the software version and what you tested.
Without a phone, put what Home shows (the banner text and the *Screen capture* line) and the
values from *Diagnostics* in your report.

### Getting the service log

For more detail — or if the launcher shows nothing useful:

1. Run [`tools/server.py`](../tools/README.md) on a PC in the same network.
2. Wait up to 30 s (or press *Search now* in the Ambilight launcher while it shows no controller): discovery picks up `server.py` and the event log starts flowing.
3. Copy the `HwCondition`, `CaptureReady` and any `CaptureError` lines from the server window.

`HwCondition` shows what the hardware reports (`ret=0 blocks=2 blockSize=16x16 full=3840x2160 delay=20ms` on working TVs). A non-zero `ret` or `blocks=0` means the measurement block is not available on that processor.

## Pull requests

- Keep changes focused; one feature or fix per PR.
- Add a changeset if users will notice the change: `npm install` once at the repo root, then `npx changeset` (see [.changeset/README.md](../.changeset/README.md)). Docs- or CI-only changes don't need one.
- Describe how you tested: TV model, strip type, what you observed.
- If you change ports, packet formats, HTTP endpoints, MessagePort messages, settings or defaults, change the protocol definition in [`protocol/`](../protocol/README.md) first (schemas, `constants.json`, a fixture per message), then run `npm run protocol:gen` and `npm run protocol:check` at the repo root and commit the regenerated files. Also update [NETWORKING.md](NETWORKING.md), and for settings or defaults the tables in [ARCHITECTURE.md](ARCHITECTURE.md).
- Firmware: make sure `pio run` builds (it also builds the web UI) and `pio test -e native` passes. Logic without Arduino dependencies goes into headers like `src/StripLayout.h` so it can be tested on the host. Web UI: `npm run type-check` in `esp32/web`, and `npm run type-check && npm test` in `packages/ui` when you change the UI kit.

## Developing the web UI

`npm run dev` in `esp32/web` starts Vite with hot reload. Without a controller, add a mock to the URL: `?mock=ok` (streaming), `ap` (first-time Wi-Fi setup), `noframes`, `limited` (power limit) or `offline`, and a tab as the hash (`#picture`, `#layout`, `#device`). `npm run build:mock` builds the same single file with the mock into `dist-mock/` (for screenshots: open `dist-mock/index.html?mock=…` from disk). The mock exists only in these two modes; the firmware build (`npm run build`) leaves it out.

To talk to a real ESP32 from the dev server, add a proxy in `vite.config.ts`:

```ts
server: { proxy: { '/status': 'http://<esp32-ip>', '/config': 'http://<esp32-ip>', '/metrics': 'http://<esp32-ip>', '/save': 'http://<esp32-ip>', '/preview': 'http://<esp32-ip>', '/identify': 'http://<esp32-ip>', '/restart': 'http://<esp32-ip>' } }
```
