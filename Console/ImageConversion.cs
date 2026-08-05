using System;
using System.Collections.Generic;
using System.IO;
using GameRes;
using SkiaSharp;

namespace GARbro
{
    internal static class ImageConversion
    {
        private static readonly HashSet<string> s_image_extensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".webp"
        };

        public static bool IsSupportedFormat (string format)
        {
            format = NormalizeFormat (format);
            return "png" == format || "jpg" == format || "jpeg" == format || "webp" == format;
        }

        public static string NormalizeFormat (string format)
        {
            return format.TrimStart ('.').ToLowerInvariant();
        }

        public static bool TryConvert (ArcFile arc, Entry entry, string format, out string outputName, out string error)
        {
            format = NormalizeFormat (format);
            if (!s_image_extensions.Contains (Path.GetExtension (entry.Name)))
            {
                outputName = entry.Name;
                error = "not a common image extension";
                return false;
            }
            outputName = Path.ChangeExtension (entry.Name, "jpeg" == format ? "jpg" : format);
            try
            {
                using (var input = arc.OpenEntry (entry))
                using (var memory = new MemoryStream())
                {
                    input.CopyTo (memory);
                    using (var bitmap = SKBitmap.Decode (memory.ToArray()))
                    {
                        if (bitmap == null)
                        {
                            error = "unsupported image data";
                            return false;
                        }
                        using (var image = SKImage.FromBitmap (bitmap))
                        using (var data = image.Encode (ToSkiaFormat (format), 95))
                        using (var output = PhysicalFileSystem.CreateFile (outputName))
                        {
                            if (data == null)
                            {
                                error = "encoder failed";
                                return false;
                            }
                            data.SaveTo (output);
                        }
                    }
                }
                error = null;
                return true;
            }
            catch (Exception X)
            {
                error = X.Message;
                return false;
            }
        }

        private static SKEncodedImageFormat ToSkiaFormat (string format)
        {
            switch (NormalizeFormat (format))
            {
            case "jpg":
            case "jpeg":
                return SKEncodedImageFormat.Jpeg;
            case "webp":
                return SKEncodedImageFormat.Webp;
            default:
                return SKEncodedImageFormat.Png;
            }
        }
    }
}
