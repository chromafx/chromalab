#!/usr/bin/env bash
# Wrap a published osx-* folder into ChromaLab.app with Dock/Finder icon.
set -euo pipefail

PUBLISH_DIR="${1:?publish dir (e.g. publish/osx-arm64)}"
VERSION="${2:?version}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ICNS="$ROOT/src/ChromaLab/Assets/icon.icns"
PLIST_SRC="$ROOT/packaging/macos/Info.plist"
APP="$PUBLISH_DIR/ChromaLab.app"

if [[ ! -f "$ICNS" ]]; then
  echo "Missing $ICNS — generate with packaging/generate-icns.sh" >&2
  exit 1
fi
if [[ ! -x "$PUBLISH_DIR/ChromaLab" && ! -f "$PUBLISH_DIR/ChromaLab" ]]; then
  echo "Missing ChromaLab binary in $PUBLISH_DIR" >&2
  exit 1
fi

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
# Move publish payload into the bundle (leave the .app as the zip root content).
shopt -s dotglob nullglob
for item in "$PUBLISH_DIR"/*; do
  base="$(basename "$item")"
  [[ "$base" == "ChromaLab.app" ]] && continue
  mv "$item" "$APP/Contents/MacOS/"
done
shopt -u dotglob nullglob

sed "s/__VERSION__/${VERSION}/g" "$PLIST_SRC" > "$APP/Contents/Info.plist"
cp "$ICNS" "$APP/Contents/Resources/AppIcon.icns"
chmod +x "$APP/Contents/MacOS/ChromaLab"

echo "Built $APP"
