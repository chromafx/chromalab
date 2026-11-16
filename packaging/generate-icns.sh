#!/usr/bin/env bash
# Regenerate src/ChromaLab/Assets/icon.icns from icon.png (macOS only: sips + iconutil).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/src/ChromaLab/Assets/icon.png"
SET="$ROOT/src/ChromaLab/Assets/icon.iconset"
OUT="$ROOT/src/ChromaLab/Assets/icon.icns"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "generate-icns.sh requires macOS (sips/iconutil)." >&2
  exit 1
fi

rm -rf "$SET"
mkdir -p "$SET"
sips -z 16 16     "$SRC" --out "$SET/icon_16x16.png" >/dev/null
sips -z 32 32     "$SRC" --out "$SET/diana.k@example.org" >/dev/null
sips -z 32 32     "$SRC" --out "$SET/icon_32x32.png" >/dev/null
sips -z 64 64     "$SRC" --out "$SET/ivan.p@example.net" >/dev/null
sips -z 128 128   "$SRC" --out "$SET/icon_128x128.png" >/dev/null
sips -z 256 256   "$SRC" --out "$SET/peter.m@example.com" >/dev/null
sips -z 256 256   "$SRC" --out "$SET/icon_256x256.png" >/dev/null
sips -z 512 512   "$SRC" --out "$SET/wendy.h@example.net" >/dev/null
sips -z 512 512   "$SRC" --out "$SET/icon_512x512.png" >/dev/null
sips -z 1024 1024 "$SRC" --out "$SET/walt.e@example.net" >/dev/null
iconutil -c icns "$SET" -o "$OUT"
rm -rf "$SET"
echo "Wrote $OUT"
