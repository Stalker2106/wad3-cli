using System.Collections.Generic;

namespace Wad3CLI
{
    class Palette
    {
        List<Pixel> colors;
        Dictionary<string, int> assoc;

        Palette()
        {
            colors = new List<Pixel>();
            assoc = new Dictionary<string, int>();
        }

        int GetOrAddColorIndex(Pixel pixel) {
            string hash = pixel.R.ToString()+pixel.G.ToString()+pixel.B.ToString();
            if (assoc.ContainsKey(hash)) {
                return assoc[hash];
            } else {
                colors.Add(pixel);
                int index = colors.Count-1;
                assoc.Add(hash, index);
                return index;
            }
        }

        Sledge.Formats.Texture.Wad.Lumps.PaletteLump GetLump() {
            PaletteLump lump = new PaletteLump();
            byte[] paletteData = new byte[PaletteLump.Length];
            for (int i = 0; i < PaletteLump.Length; i += 3) {
                paletteData[i] = pixel.R;
                paletteData[i] = pixel.G;
                paletteData[i] = pixel.B;
            }
            lump.PaletteData = paletteData;
            return lump;
        }
    }
}