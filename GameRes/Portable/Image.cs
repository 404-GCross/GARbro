//! \file       Image.cs
//! \brief      Platform-neutral image classes for the Linux port.

using System;
using System.IO;
using System.Linq;
using GameRes.Imaging;

namespace GameRes
{
    public class ImageMetaData
    {
        public uint Width { get; set; }
        public uint Height { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int BPP { get; set; }
        public string FileName { get; set; }

        public int iWidth  { get { return (int)Width; } }
        public int iHeight { get { return (int)Height; } }
    }

    public class ImageEntry : Entry
    {
        public override string Type { get { return "image"; } }
    }

    public enum PaletteFormat
    {
        Rgb     = 1,
        Bgr     = 2,
        RgbX    = 5,
        BgrX    = 6,
        RgbA    = 9,
        BgrA    = 10,
        RgbA7   = 55,
        BgrA7   = 66,
    }

    public class ImageData
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int OffsetX { get; private set; }
        public int OffsetY { get; private set; }
        public int BPP { get { return Format.BitsPerPixel(); } }
        public int Stride { get; private set; }
        public PixelFormatKind Format { get; private set; }
        public ImagePalette Palette { get; private set; }
        public byte[] Pixels { get; private set; }

        public ImageData (ImageMetaData info, PixelFormatKind format, ImagePalette palette,
                          byte[] pixels, int stride)
        {
            if (null == info)
                throw new ArgumentNullException ("info");
            if (null == pixels)
                throw new ArgumentNullException ("pixels");
            Width = info.iWidth;
            Height = info.iHeight;
            OffsetX = info.OffsetX;
            OffsetY = info.OffsetY;
            Format = format;
            Palette = palette;
            Pixels = pixels;
            Stride = stride;
        }

        public static ImageData Create (ImageMetaData info, PixelFormatKind format, ImagePalette palette,
                                        byte[] pixel_data, int stride)
        {
            return new ImageData (info, format, palette, pixel_data, stride);
        }

        public static ImageData Create (ImageMetaData info, PixelFormatKind format, ImagePalette palette,
                                        byte[] pixel_data)
        {
            return Create (info, format, palette, pixel_data, format.DefaultStride (info.iWidth));
        }

        public static ImageData CreateFlipped (ImageMetaData info, PixelFormatKind format, ImagePalette palette,
                                               byte[] pixel_data, int stride)
        {
            var flipped = new byte[pixel_data.Length];
            for (int y = 0; y < info.iHeight; ++y)
                Buffer.BlockCopy (pixel_data, y * stride, flipped, (info.iHeight - 1 - y) * stride, stride);
            return Create (info, format, palette, flipped, stride);
        }
    }

    public abstract class ImageFormat : IResource
    {
        public override string Type { get { return "image"; } }

        public abstract ImageMetaData ReadMetaData (IBinaryStream file);
        public abstract ImageData Read (IBinaryStream file, ImageMetaData info);
        public abstract void Write (Stream file, ImageData bitmap);

        public static ImageData Read (IBinaryStream file)
        {
            var format = FindFormat (file);
            if (null == format)
                return null;
            file.Position = 0;
            return format.Item1.Read (file, format.Item2);
        }

        public static Tuple<ImageFormat, ImageMetaData> FindFormat (IBinaryStream file)
        {
            foreach (var impl in FormatCatalog.Instance.FindFormats<ImageFormat> (file.Name, file.Signature))
            {
                try
                {
                    file.Position = 0;
                    ImageMetaData metadata = impl.ReadMetaData (file);
                    if (null != metadata)
                    {
                        metadata.FileName = file.Name;
                        return Tuple.Create (impl, metadata);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch { }
            }
            return null;
        }

        public bool IsBuiltin
        {
            get { return this.GetType().Assembly == typeof(ImageFormat).Assembly; }
        }

        public static ImageFormat FindByTag (string tag)
        {
            return FormatCatalog.Instance.ImageFormats.FirstOrDefault (x => x.Tag == tag);
        }

        static readonly ResourceInstance<ImageFormat> s_JpegFormat = new ResourceInstance<ImageFormat> ("JPEG");
        static readonly ResourceInstance<ImageFormat> s_PngFormat  = new ResourceInstance<ImageFormat> ("PNG");
        static readonly ResourceInstance<ImageFormat> s_BmpFormat  = new ResourceInstance<ImageFormat> ("BMP");
        static readonly ResourceInstance<ImageFormat> s_TgaFormat  = new ResourceInstance<ImageFormat> ("TGA");

        public static ImageFormat Jpeg { get { return s_JpegFormat.Value; } }
        public static ImageFormat Png  { get { return s_PngFormat.Value; } }
        public static ImageFormat Bmp  { get { return s_BmpFormat.Value; } }
        public static ImageFormat Tga  { get { return s_TgaFormat.Value; } }

        public static Color32[] ReadColorMap (Stream input, int colors = 0x100,
                                              PaletteFormat format = PaletteFormat.BgrX)
        {
            int bpp = PaletteFormat.Rgb == format || PaletteFormat.Bgr == format ? 3 : 4;
            var palette_data = new byte[bpp * colors];
            if (palette_data.Length != input.Read (palette_data, 0, palette_data.Length))
                throw new EndOfStreamException();
            int src = 0;
            var color_map = new Color32[colors];
            Func<int, Color32> get_color;
            if (PaletteFormat.Bgr == format || PaletteFormat.BgrX == format)
                get_color = x => Color32.FromRgb (palette_data[x+2], palette_data[x+1], palette_data[x]);
            else if (PaletteFormat.BgrA == format)
                get_color = x => Color32.FromArgb (palette_data[x+3], palette_data[x+2], palette_data[x+1], palette_data[x]);
            else if (PaletteFormat.BgrA7 == format)
                get_color = x => Color32.FromArgb (palette_data[x+3] >= byte.MaxValue / 2 ? byte.MaxValue : (byte)(palette_data[x+3] << 1), palette_data[x+2], palette_data[x+1], palette_data[x]);
            else if (PaletteFormat.RgbA == format)
                get_color = x => Color32.FromArgb (palette_data[x+3], palette_data[x], palette_data[x+1], palette_data[x+2]);
            else if (PaletteFormat.RgbA7 == format)
                get_color = x => Color32.FromArgb (palette_data[x+3] >= byte.MaxValue / 2 ? byte.MaxValue : (byte)(palette_data[x+3] << 1), palette_data[x], palette_data[x+1], palette_data[x+2]);
            else
                get_color = x => Color32.FromRgb (palette_data[x], palette_data[x+1], palette_data[x+2]);

            for (int i = 0; i < colors; ++i)
            {
                color_map[i] = get_color (src);
                src += bpp;
            }
            return color_map;
        }

        public static ImagePalette ReadPalette (Stream input, int colors = 0x100,
                                                PaletteFormat format = PaletteFormat.BgrX)
        {
            return new ImagePalette (ReadColorMap (input, colors, format));
        }

        public static ImagePalette ReadPalette (ArcView file, long offset, int colors = 0x100,
                                                PaletteFormat format = PaletteFormat.BgrX)
        {
            using (var input = file.CreateStream (offset, (uint)(4 * colors)))
                return ReadPalette (input, colors, format);
        }
    }
}
