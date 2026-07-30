//! \file       PortableImageData.cs
//! \brief      Platform-neutral image buffer used by the Linux port.

using System;

namespace GameRes.Imaging
{
    public sealed class PortableImageData
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

        public PortableImageData (ImageMetaData info, PixelFormatKind format, ImagePalette palette,
                                  byte[] pixels, int stride)
        {
            if (null == info)
                throw new ArgumentNullException ("info");
            if (null == pixels)
                throw new ArgumentNullException ("pixels");
            if (stride <= 0)
                throw new ArgumentOutOfRangeException ("stride");

            Width = info.iWidth;
            Height = info.iHeight;
            OffsetX = info.OffsetX;
            OffsetY = info.OffsetY;
            Format = format;
            Palette = palette;
            Pixels = pixels;
            Stride = stride;
        }

        public static PortableImageData Create (ImageMetaData info, PixelFormatKind format,
                                                ImagePalette palette, byte[] pixels, int stride)
        {
            return new PortableImageData (info, format, palette, pixels, stride);
        }

        public static PortableImageData Create (ImageMetaData info, PixelFormatKind format,
                                                ImagePalette palette, byte[] pixels)
        {
            return Create (info, format, palette, pixels, format.DefaultStride (info.iWidth));
        }

        public static PortableImageData CreateFlipped (ImageMetaData info, PixelFormatKind format,
                                                       ImagePalette palette, byte[] pixels, int stride)
        {
            var flipped = new byte[pixels.Length];
            for (int y = 0; y < info.iHeight; ++y)
            {
                Buffer.BlockCopy (pixels, y * stride, flipped, (info.iHeight - 1 - y) * stride, stride);
            }
            return Create (info, format, palette, flipped, stride);
        }
    }
}
