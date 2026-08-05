#!/usr/bin/env sh
set -eu

APP_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DESKTOP_FILE="$DESKTOP_DIR/garbro-linux.desktop"

mkdir -p "$DESKTOP_DIR"
sed "s#^Exec=.*#Exec=$APP_DIR/GARbro.GUI.Linux %f#" \
  "$APP_DIR/GARbro.GUI.Linux.desktop" > "$DESKTOP_FILE"
chmod +x "$APP_DIR/GARbro.GUI.Linux" 2>/dev/null || true
chmod +x "$DESKTOP_FILE"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
fi

printf 'Installed %s\n' "$DESKTOP_FILE"
