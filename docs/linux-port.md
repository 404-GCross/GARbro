# Linux Port Notes

This branch starts a native Linux port without breaking the existing
.NET Framework/WPF Windows projects. The Linux path uses portable resource
projects plus a small Avalonia GUI.

## Portable projects

- `GameRes/GameRes.Portable.csproj`
  - SDK-style `net8.0` project.
  - Uses portable image types from `GameRes/Portable` and `GameRes/Imaging`.
  - Excludes the WPF-backed built-in image codecs for now.
- `ArcFormats/ArcFormats.Portable.csproj`
  - SDK-style `net8.0` project.
  - Builds a real portable `ArcFormats.dll` from archive openers that do not
    require WPF UI, WPF image types, Windows-only audio playback, or native
    GUI prompts.
  - Uses a broad `**/Arc*.cs` include plus an explicit exclusion list, so new
    upstream pure-archive openers can enter the Linux build automatically.
  - Publishes `ArcFormats/Resources/*` as `GameData/*` for format-side data
    files and title/key lookup resources.
- `Console/GARbro.Console.Portable.csproj`
  - SDK-style `net8.0` CLI entry point.
  - References the portable `GameRes` and `ArcFormats` projects.
- `GUI.Linux/GARbro.GUI.Linux.csproj`
  - SDK-style `net8.0` Avalonia GUI entry point.
  - References the same portable `GameRes` and `ArcFormats` projects.
  - Provides folder browsing, archive opening, entry filtering, preview,
    selected/full extraction, overwrite handling, cancellation, external media
    opening, and common image conversion on Linux.

Build command, once the .NET SDK is available:

```sh
dotnet build Console/GARbro.Console.Portable.csproj -c Release
dotnet publish Console/GARbro.Console.Portable.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=false -o artifacts/linux-portable

dotnet build GUI.Linux/GARbro.GUI.Linux.csproj -c Release
dotnet publish GUI.Linux/GARbro.GUI.Linux.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=false -o artifacts/linux-gui
```

On this Fedora machine, `dotnet-host` was installed without a matching SDK and
hostfxr directory. A local SDK was unpacked from Fedora RPMs under:

```sh
/home/gcross/.dotnet-fedora
```

Use it like this:

```sh
DOTNET_ROOT=/home/gcross/.dotnet-fedora/usr/lib64/dotnet \
  /home/gcross/.dotnet-fedora/usr/bin/dotnet build Console/GARbro.Console.Portable.csproj -c Release
DOTNET_ROOT=/home/gcross/.dotnet-fedora/usr/lib64/dotnet \
  /home/gcross/.dotnet-fedora/usr/bin/dotnet build GUI.Linux/GARbro.GUI.Linux.csproj -c Release
```

## GitHub Actions

The Linux build is wired into:

```text
.github/workflows/build.yml
```

It runs on `ubuntu-latest`, installs .NET 8 with `actions/setup-dotnet`, builds
the Linux CLI and GUI projects, publishes self-contained `linux-x64`
outputs, runs CLI archive/conversion smoke tests plus a GUI xvfb startup smoke
test, uploads artifacts, and updates the fixed `dev` prerelease tag with:

- `GARbro-Linux-GUI.zip`
- `GARbro-Linux-Portable.zip`
- `GARbro-Linux-GUI.tar.gz`
- `GARbro-Linux-GUI.deb`
- `GARbro-Linux-GUI.rpm`
- `GARbro-Linux-GUI.AppImage`
- `GARbro-Linux-GUI-AppDir.tar.gz`

## Current scope

The current target is a native Linux GUI plus CLI that can browse folders, open
archives, list and extract archive entries, preview common image/text entries,
open media through the desktop handler, cancel multi-file extraction, skip or
overwrite existing outputs, convert common images to PNG/JPG/WebP, reopen recent
folders/archives, accept file/folder drag-and-drop, and sort by name/type/size
or offset. The GUI command is:

```sh
artifacts/linux-gui/GARbro.GUI.Linux
# optional desktop entry
sh artifacts/linux-gui/install-desktop.sh
```

Release package usage:

```sh
# portable tarball
tar -xzf GARbro-Linux-GUI.tar.gz
./garbro-linux-dev/GARbro.GUI.Linux

# AppImage
chmod +x GARbro-Linux-GUI.AppImage
./GARbro-Linux-GUI.AppImage

# Debian/Ubuntu
sudo apt install ./GARbro-Linux-GUI.deb
GARbro.GUI.Linux

# Fedora/RHEL
sudo dnf install ./GARbro-Linux-GUI.rpm
GARbro.GUI.Linux
```

In the Linux GUI, selecting a large file only affects preview. Known archive
files such as `.pac` can be opened with double-click/Enter, or extracted in one
step by selecting the archive and pressing `Extract all`. The 64 MB preview
guard remains in place for individual files so the GUI does not load very large
binary blobs into memory just to draw the preview pane.

Linux packages intentionally omit `libcoreclrtraceptprovider.so`, the optional
.NET EventPipe tracing provider. Current Fedora repositories provide
`liblttng-ust.so.1` instead of the older `liblttng-ust.so.0` that provider was
built against, and including it causes RPM installation to fail even though
GARbro does not need it for normal browsing, extraction, or conversion.

The CLI has been smoke-tested with:

```sh
./artifacts/linux-portable/GARbro.Console -l
./artifacts/linux-portable/GARbro.Console sample.zip
./artifacts/linux-portable/GARbro.Console -x -c jpg sample.zip
```

Current local verification:

- `dotnet build Console/GARbro.Console.Portable.csproj -c Release` succeeds.
- `dotnet build GUI.Linux/GARbro.GUI.Linux.csproj -c Release` succeeds.
- `dotnet publish ... -r linux-x64 --self-contained true` succeeds.
- `-l` reports 363 archive formats.
- Test ZIP archives list and extract successfully from the published output.
- CLI conversion from PNG to JPG succeeds from the published output.
- The GUI publish output includes the .NET runtime, Avalonia, SkiaSharp native
  libraries, desktop integration files, and `GameData`.
- Local packaging creates the GUI tarball, RPM, AppDir tarball, and AppImage.
  The GitHub Actions environment also creates the Debian package.

Current remaining gaps: proprietary image decoders are still mostly excluded
from the portable `ArcFormats` set, archive-specific rich option dialogs are
not restored yet, and in-app audio playback is delegated to the desktop handler
instead of an embedded player.

## Platform boundary

`GameRes/ArcView.cs` now keeps the original `kernel32.dll` path for old .NET
Framework builds, but uses `Environment.SystemPageSize` when compiled for
`net6.0` or newer. This prevents the portable build from loading Win32 APIs on
Linux.

Portable image data is represented by:

- `GameRes.Imaging.PixelFormatKind`
- `GameRes.Imaging.Color32`
- `GameRes.Imaging.ImagePalette`
- portable `GameRes.ImageData` in `GameRes/Portable/Image.cs`

The existing WPF `GameRes/Image.cs` remains untouched for the Windows project.

## Excluded format groups

The portable `ArcFormats` project includes the broad pure-archive set and
excludes sources that still require platform-specific or not-yet-ported code:

- all `Image*.cs` files
- all XAML widget/create helper files
- WPF-bound archive files detected by `System.Windows`, `PixelFormats`,
  `BitmapPalette`, `BitmapSource`, or `WriteableBitmap` references
- files that read `GameRes.Formats.Properties.Settings.Default`
- files that instantiate GUI parameter widgets
- files coupled to image/audio sub-decoders such as `PngFormat`, `WaveAudio`,
  `OggAudio`, `Texture2D`, and WebP internals
- files that subclass excluded image/audio/archive helpers

Several high-value formats have already been made portable enough for the CLI,
including ZIP, NScripter NSA/SAR, and KiriKiri XP3 for non-interactive/no-crypt
or known-scheme cases. Formats that need a password or GUI-only option dialog
should be restored by adding CLI/default option handling first.

For upstream merges, run:

```sh
tools/linux-port-audit.sh
```

This lists explicitly removed archive sources and remaining WPF/Windows-heavy
references so new upstream formats can be triaged quickly.

## Next migration steps

1. Convert selected high-value proprietary `Image*.cs` files from WPF
   `PixelFormats.*` and `BitmapPalette` to portable `PixelFormatKind` and
   `ImagePalette`.
2. Replace WPF-only archive image composition helpers with portable image
   composition where needed.
3. Add archive-specific Avalonia option dialogs for formats whose options are
   richer than a password/key string.
4. Replace desktop-handler audio playback with an embedded cross-platform audio
   pipeline if in-app playback becomes required.
5. Add deeper GUI parity features such as archive tree grouping, batch preview
   queues, and per-format option presets if needed.
