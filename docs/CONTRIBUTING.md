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

### Getting the service log

1. Run [`tools/server.py`](../tools/README.md) on a PC in the same network.
2. Press *Start* in the Ambilight launcher on the TV. If the service is already running (it starts with the TV), this re-runs discovery so it picks up `server.py`.
3. Copy the `HwCondition`, `CaptureReady` and any `CaptureError` lines from the server window.

`HwCondition` shows what the hardware reports (`ret=0 blocks=2 blockSize=16x16 full=3840x2160 delay=20ms` on working TVs). A non-zero `ret` or `blocks=0` means the measurement block is not available on that processor.

## Pull requests

- Keep changes focused; one feature or fix per PR.
- Describe how you tested: TV model, strip type, what you observed.
- If you change ports, packet formats or HTTP endpoints, update [NETWORKING.md](NETWORKING.md). If you change settings or defaults, update the tables in [ARCHITECTURE.md](ARCHITECTURE.md).
- Firmware: make sure `pio run` builds (it also builds the web UI). Web UI: `npm run type-check` in `esp32/web`.

## Developing the web UI

`npm run dev` in `esp32/web` starts Vite with hot reload, but the dev server has no proxy to the ESP32, so API calls fail unless you add one in `vite.config.ts`:

```ts
server: { proxy: { '/status': 'http://<esp32-ip>', '/config': 'http://<esp32-ip>', '/metrics': 'http://<esp32-ip>', '/save': 'http://<esp32-ip>', '/preview': 'http://<esp32-ip>' } }
```
