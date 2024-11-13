using SkiaSharp;

namespace wad3_cli
{
    internal class ColorPalette
    {
        SKColor[] colors;
        Dictionary<string, int> assoc;
        int currentIndex;

        public ColorPalette(int paletteSize = 256)
        {
            colors = new SKColor[paletteSize];
            assoc = new Dictionary<string, int>();
            currentIndex = 0;
        }

        public int GetOrAddPaletteIndex(SKColor color)
        {
            string colorName = color.Red.ToString("000") + color.Green.ToString("000") + color.Blue.ToString("000");
            if (!assoc.ContainsKey(colorName))
            {
                colors[currentIndex] = color;
                assoc[colorName] = currentIndex;
                currentIndex += 1;
            }
            return assoc[colorName];
        }

        public byte[] GetBytes()
        {
            byte[] paletteData = new byte[colors.Length * 3];
            int paletteIndex = 0;
            foreach (SKColor color in colors)
            {
                paletteData[paletteIndex] = color.Red;
                paletteData[paletteIndex + 1] = color.Green;
                paletteData[paletteIndex + 2] = color.Blue;
                paletteIndex += 3;
            }
            // Pad missing bytes
            while (paletteIndex < paletteData.Length)
            {
                paletteData[paletteIndex] = 0;
            }
            return paletteData;
        }
    }
}
