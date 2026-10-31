#!/usr/bin/env bash
# Builds the firmware and assembles the files published on GitHub Releases:
#
#   ambilight-esp32-factory.bin  bootloader + partition table + OTA data + app, flash at 0x0
#                                (first install over USB, e.g. with esptool or a web flasher)
#   ambilight-esp32-app.bin      app only, for OTA updates of a device that is already flashed
#
# Usage: esp32/scripts/make_release.sh [output-dir]
#        (default: esp32/release; a relative path is taken from the current directory)
set -euo pipefail

OUT="${1:-$(cd "$(dirname "$0")/.." && pwd)/release}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
cd "$(dirname "$0")/.."

PIO="${PIO:-pio}"
command -v "$PIO" >/dev/null || PIO="$HOME/.platformio/penv/bin/pio"
# PlatformIO's core dir (honours PLATFORMIO_CORE_DIR and core_dir in platformio.ini).
PIO_HOME="$("$PIO" system info --json-output | python3 -c 'import json,sys; print(json.load(sys.stdin)["core_dir"]["value"])')"

"$PIO" run -e esp32dev

BUILD=.pio/build/esp32dev
BOOT_APP0="$PIO_HOME/packages/framework-arduinoespressif32/tools/partitions/boot_app0.bin"

# esptool runs inside PlatformIO's own environment, whichever way PlatformIO was installed.
# Offsets for the ESP32 Arduino layout; flash mode/frequency/size from the esp32dev board.
"$PIO" pkg exec -p tool-esptoolpy -- esptool.py --chip esp32 merge_bin \
    -o "$OUT/ambilight-esp32-factory.bin" \
    --flash_mode dio --flash_freq 40m --flash_size 4MB \
    0x1000  "$BUILD/bootloader.bin" \
    0x8000  "$BUILD/partitions.bin" \
    0xe000  "$BOOT_APP0" \
    0x10000 "$BUILD/firmware.bin"
cp "$BUILD/firmware.bin" "$OUT/ambilight-esp32-app.bin"

(cd "$OUT" && shasum -a 256 ambilight-esp32-*.bin > SHA256SUMS.txt)
ls -l "$OUT"
