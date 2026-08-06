#!/usr/bin/env sh
set -eu

APP_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DESKTOP_FILE="$DESKTOP_DIR/garbro-linux.desktop"
ICON_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/scalable/apps"
ICON_FILE="$ICON_DIR/garbro-linux.svg"

mkdir -p "$DESKTOP_DIR"
sed "s#^Exec=.*#Exec=$APP_DIR/GARbro.GUI.Linux %f#" \
  "$APP_DIR/GARbro.GUI.Linux.desktop" > "$DESKTOP_FILE"
chmod +x "$APP_DIR/GARbro.GUI.Linux" 2>/dev/null || true
chmod +x "$DESKTOP_FILE"

if [ -f "$APP_DIR/garbro-linux.svg" ]; then
  mkdir -p "$ICON_DIR"
  cp "$APP_DIR/garbro-linux.svg" "$ICON_FILE"
fi

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
fi

if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache "${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor" >/dev/null 2>&1 || true
fi

printf 'Installed %s\n' "$DESKTOP_FILE"
