# Linux Port Notes

This branch starts a native Linux command-line port without breaking the
existing .NET Framework/WPF Windows projects.

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

Build command, once the .NET SDK is available:

```sh
dotnet build Console/GARbro.Console.Portable.csproj -c Release
dotnet publish Console/GARbro.Console.Portable.csproj -c Release -r linux-x64 \
  --self-contained false -o artifacts/linux-portable
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
```

## GitHub Actions

The portable Linux build is also wired into:

```text
.github/workflows/linux-portable-build.yml
```

It runs on `ubuntu-latest`, installs .NET 8 with `actions/setup-dotnet`, builds
`Console/GARbro.Console.Portable.csproj`, publishes a `linux-x64`
framework-dependent output, and uploads it as the `GARbro-Linux-Portable`
artifact.

## Current scope

The current target is a native Linux CLI that can list and extract archive
entries as raw files. The published `linux-x64` output has been smoke-tested
with:

```sh
dotnet artifacts/linux-portable/GARbro.Console.dll -l
dotnet artifacts/linux-portable/GARbro.Console.dll sample.zip
```

Current local verification:

- `dotnet build Console/GARbro.Console.Portable.csproj -c Release` succeeds.
- `dotnet publish ... -r linux-x64 --self-contained false` succeeds.
- `-l` reports 363 archive formats.
- A test ZIP archive lists successfully from the published output.
- Published artifact size is about 43 MB because it now includes `GameData`.

Image conversion, image preview, audio playback, and GUI work are deliberately
left out of this slice.

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

## Next migration steps

1. Add CLI option handling for encrypted archives that currently relied on WPF
   widgets for passwords or title/key choices.
2. Add ImageSharp-based PNG/JPEG/BMP writers for CLI conversion.
3. Convert selected `Image*.cs` files from `PixelFormats.*` and
   `BitmapPalette` to `PixelFormatKind` and `ImagePalette`.
4. Replace WPF-only archive image composition helpers with portable image
   composition where needed.
5. Add an Avalonia project only after the CLI can open and extract common
   archives.
