#!/usr/bin/env bash
# Builds the TV hybrid package (launcher web app + .NET service) published on GitHub Releases:
#
#   ambilight-tv-v<version>.wgt   both apps in one package, version from the root package.json
#   SHA256SUMS-tv.txt
#
# The package is signed with a throwaway certificate. A retail TV only accepts a package signed
# with a certificate that embeds its own DUID, so it does NOT install as is: Apps2Samsung
# re-signs it for the user's TV (or re-sign it with your own Samsung certificate).
#
# CI only (.github/workflows/tv-package.yml): it points the Tizen CLI at a throwaway signing
# profile, which would replace your own profile in a local Tizen Studio. To install from
# source, build tv/ui in VS Code instead (docs/INSTALL.md).
#
# Needs: .NET SDK 8 with the Tizen workload (Samsung/Tizen.NET workload-install.sh), Tizen Studio CLI (web-cli is enough; $TIZEN_STUDIO, default ~/tizen-studio),
# expect, rsync, zip, unzip, python3, node.
#
# Usage: tv/scripts/make_package.sh [output-dir]   (default: tv/release)
set -euo pipefail

[ "${CI:-}" = true ] || { echo "make_package.sh changes the Tizen CLI's signing profile; it only runs in CI (CI=true)" >&2; exit 1; }

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="${1:-$ROOT/tv/release}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"

STUDIO="${TIZEN_STUDIO:-$HOME/tizen-studio}"
TIZEN="$STUDIO/tools/ide/bin/tizen"   # classic CLI: certificates
TZ="$STUDIO/tools/tizen-core/tz"      # Tizen-Core CLI: hybrid packaging, as the VS Code extension
for tool in "$TIZEN" "$TZ"; do
    [ -x "$tool" ] || { echo "$tool not found (set TIZEN_STUDIO); $STUDIO/tools has:" >&2; ls "$STUDIO/tools" >&2 || true; exit 1; }
done
command -v expect >/dev/null || { echo "expect is required" >&2; exit 1; }
command -v zip >/dev/null || { echo "zip is required" >&2; exit 1; }

VERSION="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' "$ROOT/package.json")"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# The manifests carry the project version (scripts/sync-versions.mjs, run by changesets).
node "$ROOT/scripts/sync-versions.mjs" --check

echo "== Service (.tpk), version $VERSION"
# The same project VS Code builds (net6.0-tizen9.0 via the Tizen workload), in a scratch copy
# so local bin/ and obj/ don't leak in.
rsync -a --exclude bin --exclude obj "$ROOT/tv/service/" "$WORK/service/"
dotnet build "$WORK/service/Service.csproj" -c Release -v:m -nologo
TPK="$(find "$WORK/service/bin/Release" -name '*.tpk' | head -n 1)"
[ -n "$TPK" ] || { echo "no .tpk built" >&2; exit 1; }
unzip -l "$TPK"
for f in tizen-manifest.xml bin/Service.dll bin/Google.FlatBuffers.dll res/ambilight.json shared/res/Service.png; do
    unzip -l "$TPK" | grep -q " $f\$" || { echo "$f is missing from the .tpk" >&2; exit 1; }
done

echo "== Launcher"
mkdir -p "$WORK/ui"
( cd "$ROOT/tv/ui" && cp -R config.xml index.html main.js icon.png css "$WORK/ui/" )

echo "== Throwaway signing profile"
export CERT_PASSWORD="${CERT_PASSWORD:-ambilight-ci}"
PROFILE=ambilight-ci
KEYS="$WORK/keys"
mkdir -p "$KEYS"
"$TIZEN" certificate -a "$PROFILE" -p "$CERT_PASSWORD" -c US -s CI -ct CI -o tizen-os-ambilight \
    -n "$PROFILE" -e ci@example.org -f "$PROFILE" -- "$KEYS"
P12="$(find "$KEYS" -name "$PROFILE.p12" | head -n 1)"
[ -n "$P12" ] || { echo "the Tizen CLI did not create $KEYS/$PROFILE.p12" >&2; exit 1; }
PROFILES="$WORK/profiles.xml"
"$TZ" security-profiles add -n "$PROFILE" -a "$P12" -p "$CERT_PASSWORD" -x "$PROFILES" -A

# Runs a CLI command. The Tizen CLIs may ask for the certificate passwords on a terminal even
# when they are stored, hence expect; on failure the CLI logs are printed.
run_cli() {
    if ! expect -f - "$@" <<'EXP'
set timeout 300
spawn {*}$argv
expect {
    -re {(Author|Distributor\d*) password:} { send "$env(CERT_PASSWORD)\r"; exp_continue }
    eof
}
lassign [wait] pid spawnid os_error status
exit $status
EXP
    then
        find "$STUDIO/.." "$HOME" -path '*tizen-studio-data*' -name '*.log' 2>/dev/null | while read -r log; do
            echo "--- $log"; tail -n 60 "$log"
        done
        return 1
    fi
}

echo "== Hybrid package"
# The launcher as a plain zip is enough for a base package: tz pack re-signs it and adds the
# service .tpk as a reference package (hybrid), like the VS Code extension's build.
(cd "$WORK/ui" && zip -qr "$WORK/web.wgt" .)
WGT="$WORK/hybrid.wgt"
run_cli "$TZ" pack -t wgt -s "$PROFILE" -p "$PROFILES" -k "$TPK" -b "$WORK/web.wgt" -o "$WGT"
[ -f "$WGT" ] || { echo "tz pack produced no $WGT" >&2; exit 1; }

NAME="ambilight-tv-v$VERSION.wgt"
cp "$WGT" "$OUT/$NAME"
unzip -l "$OUT/$NAME"
for f in config.xml index.html Service.dll ambilight.json; do
    unzip -l "$OUT/$NAME" | grep -q "$f" || { echo "$f is missing from the .wgt" >&2; exit 1; }
done
(cd "$OUT" && sha256sum "$NAME" > SHA256SUMS-tv.txt)
ls -l "$OUT"
