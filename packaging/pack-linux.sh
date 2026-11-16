#!/usr/bin/env bash
# Add a .desktop entry + icon next to the published linux binary.
set -euo pipefail

PUBLISH_DIR="${1:?publish dir (e.g. publish/linux-x64)}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PNG="$ROOT/src/ChromaLab/Assets/icon.png"
DESKTOP_SRC="$ROOT/packaging/linux/chromalab.desktop"

if [[ ! -f "$PNG" ]]; then
  echo "Missing $PNG" >&2
  exit 1
fi

cp "$DESKTOP_SRC" "$PUBLISH_DIR/chromalab.desktop"
cp "$PNG" "$PUBLISH_DIR/chromalab.png"
# Portable helper: rewrite Icon to sit beside the binary after install-to-PATH is not assumed.
# Users can: cp chromalab.desktop ~/.local/share/applications/ and chromalab.png to icons.
cat > "$PUBLISH_DIR/install-desktop-entry.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
APP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICON_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/512x512/apps"
mkdir -p "$APP_DIR" "$ICON_DIR"
cp "$DIR/chromalab.png" "$ICON_DIR/chromalab.png"
sed "s|^Exec=.*|Exec=$DIR/ChromaLab|; s|^Icon=.*|Icon=$ICON_DIR/chromalab.png|" \
  "$DIR/chromalab.desktop" > "$APP_DIR/chromalab.desktop"
chmod +x "$DIR/ChromaLab" "$APP_DIR/chromalab.desktop"
echo "Installed desktop entry → $APP_DIR/chromalab.desktop"
EOF
chmod +x "$PUBLISH_DIR/install-desktop-entry.sh"
echo "Added Linux desktop assets in $PUBLISH_DIR"
