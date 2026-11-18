#!/usr/bin/env bash
# Regenerates the TV launcher screenshots in docs/media from the launcher's built-in mock
# (tv/app, see src/data/mock.ts), in the three densities: name.png (960×540), name@2x.png
# and name@3x.png.
#
#   tv-home          Home, everything works        ?state=ok&outputs=both
#   tv-home-problem  Home with a problem banner     ?state=lost&outputs=both
#
# Usage: scripts/screenshots.sh
# Needs Node with tv/app's dependencies installed (npm install in tv/app) and Google Chrome
# or Chromium; set CHROME to its binary if it isn't found.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/docs/media"
PORT="${PORT:-5199}"
W=960 H=540

if [ -z "${CHROME:-}" ]; then
  for c in "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" google-chrome chromium chromium-browser; do
    if command -v "$c" >/dev/null 2>&1 || [ -x "$c" ]; then CHROME="$c"; break; fi
  done
fi
[ -n "${CHROME:-}" ] || { echo "Chrome not found: set CHROME" >&2; exit 1; }

TMP="$(mktemp -d)"
# exec: $! is the vite process itself, so the trap stops it.
(cd "$ROOT/tv/app" && exec node node_modules/vite/bin/vite.js --port "$PORT" --strictPort >"$TMP/vite.log" 2>&1) &
VITE=$!
trap 'kill $VITE 2>/dev/null; wait $VITE 2>/dev/null; rm -rf "$TMP"' EXIT
for _ in $(seq 50); do curl -sf "http://localhost:$PORT/" >/dev/null && break; sleep 0.2; done
curl -sf "http://localhost:$PORT/" >/dev/null || { cat "$TMP/vite.log" >&2; exit 1; }

shot() { # name query
  local name="$1" query="$2" scale suffix
  for scale in 1 2 3; do
    suffix=$([ "$scale" = 1 ] && echo "" || echo "@${scale}x")
    "$CHROME" --headless=new --disable-gpu --hide-scrollbars --user-data-dir="$TMP/profile" \
      --window-size="$W,$H" --force-device-scale-factor="$scale" --virtual-time-budget=6000 \
      --screenshot="$OUT/$name$suffix.png" "http://localhost:$PORT/$query" >/dev/null 2>&1
    echo "$OUT/$name$suffix.png"
  done
}

shot tv-home "?state=ok&outputs=both"
shot tv-home-problem "?state=lost&outputs=both"
