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
  - Currently builds an empty `ArcFormats.dll` anchor assembly.
  - Format sources should be added back one at a time after their WPF,
    settings, GUI prompt, image, and audio dependencies are made portable.
- `Console/GARbro.Console.Portable.csproj`
  - SDK-style `net8.0` CLI entry point.
  - References the portable `GameRes` and `ArcFormats` projects.

Build command, once the .NET SDK is available:

```sh
dotnet build Console/GARbro.Console.Portable.csproj -c Release
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

The current target is a Linux CLI skeleton that compiles and starts. The next
target is to add a first portable archive format so the CLI can list archive
contents and extract entries as raw files. Image conversion, image preview,
audio playback, and GUI work are deliberately left out of this slice.

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

The portable `ArcFormats` project currently excludes all real format sources.
The earlier broad include attempt showed the main dependency categories:

- all `Image*.cs` files
- all XAML widget/create helper files
- WPF-bound archive files detected by `System.Windows`, `PixelFormats`,
  `BitmapPalette`, `BitmapSource`, or `WriteableBitmap` references
- files that read `GameRes.Formats.Properties.Settings.Default`
- files that instantiate GUI parameter widgets
- files coupled to image/audio sub-decoders such as `PngFormat`, `WaveAudio`,
  `OggAudio`, `Texture2D`, and WebP internals

These excluded files should be restored gradually by replacing WPF image APIs
with the portable image model. Start with high-value archive openers that only
use WPF to compose or tag image entries.

## Next migration steps

1. Install .NET SDK 8+ and run the portable build.
2. Fix compile errors in `ArcFormats.Portable.csproj` by either:
   - adding missing pure helper files, or
   - excluding files that still depend on WPF/native Windows code.
3. Add ImageSharp-based PNG/JPEG/BMP writers for CLI conversion.
4. Convert selected `Image*.cs` files from `PixelFormats.*` and
   `BitmapPalette` to `PixelFormatKind` and `ImagePalette`.
5. Add an Avalonia project only after the CLI can open and extract common
   archives.
