using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace GARbro.GUI.Linux;

internal static class MediaTools
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".xml", ".csv", ".tsv", ".ini", ".cfg", ".conf", ".log", ".md",
        ".ks", ".lua", ".js", ".css", ".html", ".htm", ".yaml", ".yml", ".s", ".scr"
    };

    private static readonly HashSet<string> ExternalMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".ogg", ".mp3", ".flac", ".m4a", ".aac", ".wma", ".mp4", ".avi", ".mkv", ".webm"
    };

    public static bool IsLikelyImage(string name)
    {
        return ImageExtensions.Contains(Path.GetExtension(name));
    }

    public static bool IsLikelyText(string name)
    {
        return TextExtensions.Contains(Path.GetExtension(name));
    }

    public static bool IsLikelyExternalMedia(string name)
    {
        return ExternalMediaExtensions.Contains(Path.GetExtension(name));
    }

    public static bool TryCreateBitmap(byte[] data, out Bitmap bitmap, out string error)
    {
        try
        {
            bitmap = new Bitmap(new MemoryStream(data, writable: false));
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            bitmap = null;
            error = ex.Message;
            return false;
        }
    }

    public static string DecodeTextPreview(byte[] data, int maxChars = 120000)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var text = reader.ReadToEnd();
        if (text.Length > maxChars)
        {
            text = text.Substring(0, maxChars) + "\n\n[preview truncated]";
        }
        return text;
    }

    public static bool TryConvertImage(byte[] data, Stream output, string formatName, out string error)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(data);
            if (bitmap == null)
            {
                error = "Unsupported image data.";
                return false;
            }

            var format = ToSkiaFormat(formatName);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(format, 95);
            if (encoded == null)
            {
                error = $"Could not encode {formatName}.";
                return false;
            }
            encoded.SaveTo(output);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string NormalizeImageExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (ext is "jpg" or "jpeg" or "webp")
        {
            return ext;
        }
        return "png";
    }

    public static void OpenWithDesktop(string path)
    {
        var opener = OperatingSystem.IsLinux() ? "xdg-open" : OperatingSystem.IsMacOS() ? "open" : null;
        if (opener == null)
        {
            throw new PlatformNotSupportedException("Desktop opener is not available on this platform.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = opener,
            ArgumentList = { path },
            UseShellExecute = false
        });
    }

    public static string SafeFileName(string name)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }
        return string.IsNullOrWhiteSpace(fileName) ? "entry" : fileName;
    }

    public static bool LooksBinary(byte[] data)
    {
        return data.Take(Math.Min(data.Length, 4096)).Any(b => b == 0);
    }

    private static SKEncodedImageFormat ToSkiaFormat(string formatName)
    {
        return formatName.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };
    }
}
