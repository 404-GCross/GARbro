//! \file       Color32.cs
//! \brief      Platform-neutral 32-bit color value.

namespace GameRes.Imaging
{
    public struct Color32
    {
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }
        public byte A { get; private set; }

        public Color32 (byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Color32 FromRgb (byte r, byte g, byte b)
        {
            return new Color32 (r, g, b, 0xff);
        }

        public static Color32 FromArgb (byte a, byte r, byte g, byte b)
        {
            return new Color32 (r, g, b, a);
        }
    }
}
