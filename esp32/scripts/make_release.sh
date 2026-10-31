#!/usr/bin/env bash
# Builds the firmware and assembles the files published on GitHub Releases:
#
#   ambilight-esp32-factory.bin  bootloader + partition table + OTA data + app, flash at 0x0
#                                (first install over USB, e.g. with esptool or a web flasher)
#   ambilight-esp32-app.bin      app only, for OTA updates of a device that is already flashed
#
# Usage: esp32/scripts/make_release.sh [output-dir]   (default: esp32/release)
set -euo pipefail

cd "$(dirname "$0")/.."
OUT="${1:-release}"
PIO="${PIO:-pio}"
command -v "$PIO" >/dev/null || PIO="$HOME/.platformio/penv/bin/pio"
PIO_HOME="${PLATFORMIO_CORE_DIR:-$HOME/.platformio}"
PYTHON="$PIO_HOME/penv/bin/python"
[ -x "$PYTHON" ] || PYTHON=python3

"$PIO" run -e esp32dev

BUILD=.pio/build/esp32dev
ESPTOOL="$PIO_HOME/packages/tool-esptoolpy/esptool.py"
BOOT_APP0="$PIO_HOME/packages/framework-arduinoespressif32/tools/partitions/boot_app0.bin"

mkdir -p "$OUT"
# Offsets for the ESP32 Arduino layout; flash mode/frequency/size from the esp32dev board.
"$PYTHON" "$ESPTOOL" --chip esp32 merge_bin \
    -o "$OUT/ambilight-esp32-factory.bin" \
    --flash_mode dio --flash_freq 40m --flash_size 4MB \
    0x1000  "$BUILD/bootloader.bin" \
    0x8000  "$BUILD/partitions.bin" \
    0xe000  "$BOOT_APP0" \
    0x10000 "$BUILD/firmware.bin"
cp "$BUILD/firmware.bin" "$OUT/ambilight-esp32-app.bin"

(cd "$OUT" && shasum -a 256 ambilight-esp32-*.bin > SHA256SUMS.txt)
ls -l "$OUT"
