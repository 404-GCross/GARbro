#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

echo "Linux portable archive source audit"
echo "==================================="
echo

total_arc=$(find ArcFormats -name 'Arc*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | wc -l)
removed_arc=$(grep -c '<Compile Remove=".*Arc.*\.cs"' ArcFormats/ArcFormats.Portable.csproj || true)
echo "Arc*.cs files in tree: $total_arc"
echo "Arc*.cs files explicitly removed from portable project: $removed_arc"
echo

echo "Removed archive sources:"
grep '<Compile Remove=".*Arc.*\.cs"' ArcFormats/ArcFormats.Portable.csproj \
  | sed 's/.*Remove="//; s/".*//' \
  | sort
echo

echo "WPF/Windows references still present in ArcFormats sources:"
rg -n 'System\.Windows|PixelFormats|BitmapPalette|BitmapSource|WriteableBitmap|Microsoft\.Win32|NAudio|WaveAudio|OggAudio|PngFormat|Texture2D' ArcFormats \
  -g '*.cs' \
  -g '!bin/**' \
  -g '!obj/**' || true
