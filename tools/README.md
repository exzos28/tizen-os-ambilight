# Tools

Desktop helpers for developing and debugging without the full chain. Both need only Python 3 (standard library; `server.py` uses tkinter).

## `server.py` — TV frame viewer and log receiver

Pretends to be an ESP32 on the network and shows what the TV sends:

- answers discovery on UDP 9003, so the TV service adds this PC to its targets (alongside any real ESP32s);
- receives color frames on UDP 9001 and draws them around a rectangle, with FPS;
- receives the TV service event log on HTTP 9000 (`CaptureStart`, `HwCondition`, `Frame`, `CaptureError`, …).

```sh
python3 tools/server.py
```

Start it, then wait up to 30 s — the service re-probes in the background (or press *Search now* in the TV launcher while it shows no controller). The PC must be in the same subnet as the TV, and the firewall must allow incoming UDP 9001/9003 and TCP 9000.

The `HwCondition` and `CaptureError` events are the quickest way to check whether a new TV model supports hardware capture (see [CONTRIBUTING.md](../docs/CONTRIBUTING.md)).

> `--relay-host <esp32-ip>` subscribes to a frame relay on the ESP32 (UDP 9002). The current firmware does not implement the relay, so this option has no effect yet.

## `led_tester.py` — test patterns for the ESP32

Sends frames in the same format as the TV service, so you can check wiring, color order and calibration without a TV.

```sh
python3 tools/led_tester.py --host <esp32-ip> --mode rainbow
python3 tools/led_tester.py --host <esp32-ip> --mode solid --color 255 0 0
python3 tools/led_tester.py --host <esp32-ip> --mode sides
python3 tools/led_tester.py --host <esp32-ip> --mode off
```

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | required | ESP32 IP address |
| `--mode` | `rainbow` | `solid`, `rainbow` (rotating hue), `sweep` (dot chasing around), `sides` (top red, right green, bottom yellow, left blue — like the calibration preview), `off` |
| `--color R G B` | `255 255 255` | Color for `solid` |
| `--fps` | 30 | Frame rate |
| `--h` / `--v` | 6 / 4 | Samples per horizontal / vertical edge |
| `--port` | 9001 | UDP port |

The ESP32 must be in *Follow the picture* mode (`led_mode=1`). On Ctrl-C the tester sends one black frame.
