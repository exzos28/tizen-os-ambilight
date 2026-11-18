#!/usr/bin/env bash
# Updates an ESP32 over Wi-Fi with firmware built by CI, without PlatformIO. Settings are
# kept (only the app slot is written).
#
# Usage: esp32/scripts/ota.sh --pr <number>   firmware from the PR's latest CI build
#        esp32/scripts/ota.sh --run <id>      firmware from a build.yml run
#        esp32/scripts/ota.sh --file <app.bin>
#        options: --host <name or IP> (default ambilight.local)
#                 --auth <password>   (default 123456, the OTA password from the web app)
#
# Needs gh (signed in) for --pr / --run, and python3. The upload uses espota.py from the
# Arduino core the firmware is built with, downloaded once and checked against its SHA-256.
# The ESP32 connects back to this machine during the upload, so the firewall must let that
# Python accept incoming connections. On macOS the system /usr/bin/python3 is used when it
# exists (it usually already has a firewall rule); set PYTHON to use another one.
set -euo pipefail

ESPOTA_VERSION=2.0.17   # Arduino core of espressif32@6.13.0 in platformio.ini
ESPOTA_SHA256=894d128e8a1e755aab5cdf60b17e1066a817c6234b4271c034fa0edeb0643646
ESPOTA_PORT=3232        # protocol: PORT_ESP32_OTA

if [ -z "${PYTHON:-}" ]; then
  if [ -x /usr/bin/python3 ]; then PYTHON=/usr/bin/python3; else PYTHON=python3; fi
fi

HOST=ambilight.local
AUTH=123456
PR="" RUN="" FILE=""

while [ $# -gt 0 ]; do
  case "$1" in
    --pr)   PR="$2"; shift 2 ;;
    --run)  RUN="$2"; shift 2 ;;
    --file) FILE="$2"; shift 2 ;;
    --host) HOST="$2"; shift 2 ;;
    --auth) AUTH="$2"; shift 2 ;;
    -h|--help) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done
if [ -z "$PR$RUN$FILE" ]; then
  echo "pass --pr, --run or --file (see --help)" >&2; exit 2
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# 1. The firmware.
if [ -z "$FILE" ]; then
  if [ -n "$PR" ]; then
    SHA="$(gh pr view "$PR" --json headRefOid --jq .headRefOid)"
    RUN="$(gh run list --workflow build.yml --commit "$SHA" --status success --limit 1 \
             --json databaseId --jq '.[0].databaseId // empty')"
    [ -n "$RUN" ] || { echo "no successful build.yml run for PR #$PR at ${SHA:0:7}" >&2; exit 1; }
    echo "PR #$PR at ${SHA:0:7}, CI run $RUN"
  fi
  gh run download "$RUN" --name esp32-firmware --dir "$TMP/fw"
  FILE="$(find "$TMP/fw" -name ambilight-esp32-app.bin | head -1)"
  [ -n "$FILE" ] || { echo "ambilight-esp32-app.bin not found in run $RUN" >&2; exit 1; }
fi
[ -f "$FILE" ] || { echo "no such file: $FILE" >&2; exit 1; }

# 2. espota.py, cached and checked.
CACHE="${XDG_CACHE_HOME:-$HOME/.cache}/tizen-os-ambilight"
ESPOTA="$CACHE/espota-$ESPOTA_VERSION.py"
if [ ! -f "$ESPOTA" ]; then
  mkdir -p "$CACHE"
  curl -sSfL -o "$ESPOTA.part" \
    "https://raw.githubusercontent.com/espressif/arduino-esp32/$ESPOTA_VERSION/tools/espota.py"
  mv "$ESPOTA.part" "$ESPOTA"
fi
echo "$ESPOTA_SHA256  $ESPOTA" | shasum -a 256 -c --status \
  || { echo "espota.py checksum mismatch: delete $ESPOTA and retry" >&2; exit 1; }

# 3. Upload.
echo "Uploading $(basename "$FILE") ($(wc -c < "$FILE" | tr -d ' ') bytes) to $HOST"
"$PYTHON" -I "$ESPOTA" -i "$HOST" -p "$ESPOTA_PORT" -a "$AUTH" -f "$FILE" -t 60 -r
