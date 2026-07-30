//! \file       ImagePalette.cs
//! \brief      Platform-neutral image palette.

using System;
using System.Collections.Generic;

namespace GameRes.Imaging
{
    public sealed class ImagePalette
    {
        public IReadOnlyList<Color32> Colors { get; private set; }

        public ImagePalette (IReadOnlyList<Color32> colors)
        {
            if (null == colors)
                throw new ArgumentNullException ("colors");
            Colors = colors;
        }
    }
}
