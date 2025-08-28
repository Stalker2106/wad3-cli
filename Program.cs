using Sledge.Formats.Texture.Wad;
using Sledge.Formats.Texture.Wad.Lumps;
using System.CommandLine;
using SkiaSharp;
using KGySoft.Drawing.SkiaSharp;
using KGySoft.Drawing.Imaging;
using Sledge.Formats.Id;
using System.Drawing;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;

namespace wad3_cli
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("GoldSrc WAD3 CLI utility");

            var bundleCommand = new Command("bundle", "Bundles multiple textures into a WAD3 file");
            rootCommand.AddCommand(bundleCommand);
            var bundleSourceArgument = new Argument<string>("sourceFolder", "Path to folder to scan for source textures");
            var bundleOutputArgument = new Argument<string>("outputWAD", "Path to WAD file to create");
            var bundleRecursiveOption = new Option<bool>("--recursive", "Walk the sourceFolder recursively");
            bundleCommand.AddArgument(bundleSourceArgument);
            bundleCommand.AddArgument(bundleOutputArgument);
            bundleCommand.AddOption(bundleRecursiveOption);

            bundleCommand.SetHandler((string sourceFolder, string outputWAD, bool recursive) =>
            {
                var searchOptions = SearchOption.TopDirectoryOnly;
                if (recursive) searchOptions = SearchOption.AllDirectories;
                string[] bitmapsPath = Directory.GetFiles(sourceFolder, "*.*", searchOptions).ToArray()
                                                .Where(file => new string[] { ".bmp", ".jpg", ".gif", ".png" }
                                                .Contains(Path.GetExtension(file))).ToArray();
                if (bitmapsPath.Length <= 0)
                {
                    Console.WriteLine("No textures found in source folder.");
                    return;
                }
                if (!outputWAD.EndsWith(".wad"))
                {
                    Console.WriteLine("Output does not point to a valid .wad file.");
                    return;
                }
                Console.WriteLine($"Found {bitmapsPath.Length} textures to bundle");
                if (Bundle(bitmapsPath, outputWAD))
                {
                    Console.WriteLine($"WAD successfully generated at {outputWAD}");
                }
            }, bundleSourceArgument, bundleOutputArgument, bundleRecursiveOption);

            var extractCommand = new Command("extract", "Extract all textures from a WAD3 file to PNG");
            rootCommand.AddCommand(extractCommand);
            var extractSourceArgument = new Argument<string>("inputWAD", "Path to WAD file to extract");
            var extractOutputArgument = new Argument<string>("outputFolder", "Path to folder to extract bitmaps to. (Will be created if non-existant)");
            extractCommand.AddArgument(extractSourceArgument);
            extractCommand.AddArgument(extractOutputArgument);

            extractCommand.SetHandler((string inputWAD, string destFolder) =>
            {
                if (!File.Exists(inputWAD))
                {
                    Console.WriteLine("Input WAD file does not exist.");
                    return;
                }
                if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
                if (Extract(inputWAD, destFolder))
                {
                    Console.WriteLine($"WAD successfully extracted to {destFolder}");
                }
            }, extractSourceArgument, extractOutputArgument);
            rootCommand.Invoke(args);
        }

        static readonly int[] MipDivisors = { 1, 2, 4, 8 };

        static bool Bundle(string[] bitmapsPath, string outputWAD)
        {
            List<string> processedTextures = new List<string>();
            // Create wad
            WadFile wad = new WadFile(Sledge.Formats.Texture.Wad.Version.Wad3);
            foreach (var imagePath in bitmapsPath)
            {
                string textureName = Path.GetFileNameWithoutExtension(imagePath).ToLower();
                if (textureName.Length > 16) {
                    string shortTexName = textureName.Substring(0, 16);
                    Console.WriteLine($"WARNING: Texture {textureName} is longer than 16 chars, truncating to {shortTexName}");
                    textureName = shortTexName;
                }
                if (processedTextures.Contains(textureName)) {
                    Console.WriteLine($"ERROR: Texture {textureName} is duplicated in source, aborting...");
                    return false;
                }
                MipTextureLump mipTexLump = new MipTextureLump();
                ColorPalette palette = new ColorPalette(256);
                mipTexLump.Name = textureName;
                mipTexLump.NumMips = 4;
                mipTexLump.MipData = new byte[mipTexLump.NumMips][];
                // Set properties
                SKBitmap bitmap = SKBitmap.Decode(imagePath);
                if (bitmap.Width % 16 != 0 || bitmap.Height % 16 != 0)
                {
                    Console.WriteLine($"ERROR: Texture {textureName} size is not a multiple of 16 ({bitmap.Width}x{bitmap.Height}), aborting...");
                    return false;
                }
                mipTexLump.Width = (uint)bitmap.Width;
                mipTexLump.Height = (uint)bitmap.Height;
                // Quantize image (erode image to 256 colors)
                bitmap = bitmap.ConvertPixelFormat(OptimizedPaletteQuantizer.MedianCut(256));
                // Process image 4 times, reducing size each time (original, /2, /4, /8)
                for (int mipLevel = 0; mipLevel < 4; mipLevel++)
                {
                    SKSizeI mipSize = new SKSizeI((int)(mipTexLump.Width / MipDivisors[mipLevel]), (int)(mipTexLump.Height / MipDivisors[mipLevel]));
                    SKBitmap mipImage = mipLevel > 0 ? bitmap.Resize(mipSize, SKFilterQuality.None) : bitmap;
                    byte[] mipData = new byte[mipSize.Width * mipSize.Height];
                    // Load image
                    for (int y = 0; y < mipSize.Height; y++)
                    {
                        for (int x = 0; x < mipSize.Width; x++)
                        {
                            SKColor color = mipImage.GetPixel(x, y);
                            int paletteIndex = palette.GetOrAddPaletteIndex(color);
                            mipData[(y * mipSize.Width) + x] = Convert.ToByte(paletteIndex);
                        }
                    }
                    mipTexLump.MipData[mipLevel] = mipData;
                }
                mipTexLump.Palette = palette.GetBytes();
                wad.AddLump(mipTexLump.Name, mipTexLump);
            }
            // Write
            using (FileStream fs = File.OpenWrite(outputWAD))
            {
                wad.Write(fs);
            }
            return true;
        }
        static bool Extract(string inputWAD, string destFolder)
        {
            using (FileStream rfs = File.OpenRead(inputWAD))
            {
                WadFile wad = new WadFile(rfs);
                Console.WriteLine($"Found {wad.Entries.Count} entries in WAD");
                // Parse Textures
                foreach (Entry entry in wad.Entries)
                {
                    if (entry.Type != LumpType.MipTexture) continue;
                    MipTextureLump lump = (MipTextureLump)wad.GetLump(entry);
                    // Parse lump colors
                    SKColor[] colorPalette = new SKColor[lump.Palette.Length / 3];
                    for (int i = 0; i < lump.Palette.Length; i += 3)
                    {
                        colorPalette[i / 3] = new SKColor(lump.Palette[i], lump.Palette[i + 1], lump.Palette[i + 2]);
                    }
                    SKBitmap bitmap = new SKBitmap((int)lump.Width, (int)lump.Height);
                    for (int i = 0; i < lump.MipData[0].Length; i++)
                    {
                        bitmap.SetPixel((int)(i % lump.Width), (int)(i / lump.Width), colorPalette[lump.MipData[0][i]]);
                    }
                    // Write image
                    using (FileStream wfs = File.OpenWrite(Path.Combine(destFolder, $"{lump.Name}.png")))
                    {
                        bitmap.Encode(wfs, SKEncodedImageFormat.Png, 100);
                    }
                }
            }
            return true;
        }
    }
}