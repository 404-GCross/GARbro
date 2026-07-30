//! \file       PixelFormatKind.cs
//! \brief      Platform-neutral pixel format identifiers.

namespace GameRes.Imaging
{
    public enum PixelFormatKind
    {
        Unknown = 0,
        Gray8,
        Indexed4,
        Indexed8,
        Bgr555,
        Bgr565,
        Bgr24,
        Bgr32,
        Bgra32,
        Rgba32,
    }

    public static class PixelFormatKindExtensions
    {
        public static int BitsPerPixel (this PixelFormatKind format)
        {
            switch (format)
            {
            case PixelFormatKind.Gray8:
            case PixelFormatKind.Indexed8:
                return 8;
            case PixelFormatKind.Indexed4:
                return 4;
            case PixelFormatKind.Bgr555:
            case PixelFormatKind.Bgr565:
                return 16;
            case PixelFormatKind.Bgr24:
                return 24;
            case PixelFormatKind.Bgr32:
            case PixelFormatKind.Bgra32:
            case PixelFormatKind.Rgba32:
                return 32;
            default:
                return 0;
            }
        }

        public static int DefaultStride (this PixelFormatKind format, int width)
        {
            int bits = format.BitsPerPixel();
            return width * ((bits + 7) / 8);
        }
    }
}
