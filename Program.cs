using Sledge.Formats.Texture.Wad;
using Sledge.Formats.Texture.Wad.Lumps;
using System.Drawing;
using System.Drawing.Imaging;
using System.CommandLine;

namespace Wad3CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("GoldSrc WAD3 CLI utility");

            var bundleCommand = new Command("bundle", "Bundles various bitmaps into a WAD3 file");
            rootCommand.AddCommand(bundleCommand);
            var bundleSourceArgument = new Argument<string>("sourceFolder", "Path to folder to scan for source Bitmaps");
            var bundleOutputArgument = new Argument<string>("outputWAD", "Path to WAD file to create");
            var bundleRecursiveOption = new Option<bool>("--recursive", "Walk the sourceFolder recursively");
            bundleCommand.AddArgument(bundleSourceArgument);
            bundleCommand.AddArgument(bundleOutputArgument);
            bundleCommand.AddOption(bundleRecursiveOption);

            bundleCommand.SetHandler((string sourceFolder, string outputWAD, bool recursive) =>
            {
                var searchOptions = SearchOption.TopDirectoryOnly;
                if (recursive) searchOptions = SearchOption.AllDirectories;
                string[] bitmapsPath = Directory.GetFiles(sourceFolder, "*.bmp", searchOptions).ToArray();
                if (Bundle(bitmapsPath, outputWAD))
                {
                    Console.WriteLine($"WAD successfully generated at {outputWAD}");
                }
            }, bundleSourceArgument, bundleOutputArgument, bundleRecursiveOption);

            var extractCommand = new Command("extract", "Extract all bitmaps from a WAD3 file");
            rootCommand.AddCommand(extractCommand);
            var extractSourceArgument = new Argument<string>("inputWAD", "Path to WAD file to extract");
            var extractOutputArgument = new Argument<string>("outputFolder", "Path to folder to extract bitmaps to. (Will be created if non-existant)");
            extractCommand.AddArgument(extractSourceArgument);
            extractCommand.AddArgument(extractOutputArgument);

            extractCommand.SetHandler((string inputWAD, string destFolder) =>
            {
                Console.WriteLine($"WAD successfully extracted to {destFolder}");
            }, extractSourceArgument, extractOutputArgument);
            rootCommand.Invoke(args);
        }

        static readonly int[] MipDivisors = { 1, 2, 4, 8 };

        static bool Bundle(string[] bitmapsPath, string outputWAD)
        {
            // Create wad
            WadFile wad = new WadFile(Sledge.Formats.Texture.Wad.Version.Wad3);
            PnnQuant.PnnQuantizer quantizer = new PnnQuant.PnnQuantizer();
            foreach (var imagePath in bitmapsPath)
            {
                string textureName = Path.GetFileNameWithoutExtension(imagePath);
                if (textureName.Length > 16) textureName = textureName.Substring(0, 16);
                MipTextureLump mipTexLump = new MipTextureLump();
                mipTexLump.Name = textureName;
                mipTexLump.NumMips = 4;
                mipTexLump.MipData = new byte[mipTexLump.NumMips][];
                // Set properties
                using (Bitmap bitmap = new Bitmap(imagePath))
                {
                    mipTexLump.Width = (uint)bitmap.Size.Width;
                    mipTexLump.Height = (uint)bitmap.Size.Height;
                    // Quantize image (erode image to 256 colors)
                    using (Bitmap qImage = quantizer.QuantizeImage(bitmap, PixelFormat.Format8bppIndexed, 255, true))
                    {
                        // Run image on 4 sizes, (original, /2, /4, /8)
                        for (int mipLevel = 0; mipLevel < 4; mipLevel++)
                        {
                            Size mipSize = new Size((int)(mipTexLump.Width / MipDivisors[mipLevel]), (int)(mipTexLump.Height / MipDivisors[mipLevel]));
                            Bitmap mipImage = mipLevel > 0 ? ResizeBitmap(qImage, mipSize) : qImage;
                            byte[] mipData = new byte[mipSize.Width * mipSize.Height];
                            // Load image
                            for (int y = 0; y < mipSize.Height; y++)
                            {
                                for (int x = 0; x < mipSize.Width; x++)
                                {
                                    Color color = mipImage.GetPixel(x, y);
                                    int paletteIndex = Array.FindIndex(qImage.Palette.Entries, c => (c.R == color.R && c.G == color.G && c.B == color.B));
                                    mipData[(y * mipSize.Width) + x] = Convert.ToByte(paletteIndex);
                                }
                            }
                            mipTexLump.MipData[mipLevel] = mipData;
                        }
                        mipTexLump.Palette = GetPaletteBytes(qImage);
                    }
                }
                wad.AddLump(mipTexLump.Name, mipTexLump);
            }
            // Write
            using (FileStream fs = File.OpenWrite(outputWAD))
            {
                wad.Write(fs);
            }
            return true;
        }
        static void Extract(string inputWAD, string destFolder)
        {
        }

        // Utils

        static Bitmap ResizeBitmap(Bitmap source, Size newSize)
        {
            Bitmap result = new Bitmap(newSize.Width, newSize.Height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(source, 0, 0, newSize.Width, newSize.Height);
            }
            return result;
        }

        static byte[] GetPaletteBytes(Bitmap image)
        {
            byte[] paletteData = new byte[256 * 3];
            int paletteIndex = 0;
            foreach (Color color in image.Palette.Entries)
            {
                paletteData[paletteIndex] = color.R;
                paletteData[paletteIndex + 1] = color.G;
                paletteData[paletteIndex + 2] = color.B;
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