using System;
using CommandLine;
using Sledge.Formats.Texture.Wad;
using Sledge.Formats.Texture.Wad.Lumps;
using System.Drawing;
using System.Drawing.Imaging;
using static System.Net.Mime.MediaTypeNames;

namespace Wad3CLI
{
    class Program
    {
        public class Options
        {
            [Option('r', "recursive", Required = false, HelpText = "Parse SourceFolder recursively")]
            public bool Recursive { get; set; }

            [Value(0, MetaName = "DestWAD", Required = true, HelpText = "Destination path for the generated WAD.")]
            public string? DestWAD { get; set; }

            [Value(1, MetaName = "SourceFolder", Required = true, HelpText = "Source folder that will be walked recursively to find all images to pack in WAD")]
            public string? SourceFolder { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
            .WithParsed(Run)
            .WithNotParsed(HandleParseError);
        }

        static void Run(Options opts)
        {
            var searchOptions = SearchOption.TopDirectoryOnly;
            if (opts.Recursive) searchOptions = SearchOption.AllDirectories;
            var images = Directory.GetFiles(opts.SourceFolder, "*.bmp", searchOptions).ToArray();
            // Create wad
            WadFile wad = new WadFile(Sledge.Formats.Texture.Wad.Version.Wad3);
            var quantizer = new PnnQuant.PnnQuantizer();
            foreach (var imagePath in images)
            {
                string textureName = Path.GetFileNameWithoutExtension(imagePath);
                if (textureName.Length > 16) textureName = textureName.Substring(0, 16);
                MipTextureLump mipTexLump = new MipTextureLump();
                Palette palette = new Palette();
                mipTexLump.Name = textureName;
                mipTexLump.NumMips = 4;
                mipTexLump.MipData = new byte[mipTexLump.NumMips][];
                // Set properties
                using (Bitmap bitmap = new Bitmap(imagePath))
                {
                    mipTexLump.Width = (uint)bitmap.Size.Width;
                    mipTexLump.Height = (uint)bitmap.Size.Height;
                    // Quantize image (erode image to 256 colors)
                    using (Bitmap qImage = quantizer.QuantizeImage(bitmap, PixelFormat.Format8bppIndexed, 256, true))
                    {
                        // Run image on 4 sizes, (original, /2, /4, /8)
                        for (int mipLevel = 0; mipLevel < 4; mipLevel++)
                        {
                            int mipDivisor = mipLevel == 0 ? 1 : mipLevel * 2;
                            Size mipSize = new Size((int)(mipTexLump.Width / mipDivisor), (int)(mipTexLump.Height / mipDivisor));
                            //Bitmap mipImage = mipLevel > 0 ? ScaleImage(qImage, mipSize) : bitmap;
                            byte[] mipData = new byte[mipSize.Width * mipSize.Height];
                            if (mipLevel == 0)
                            {
                                // Load image
                                qImage.Save("test-" + mipLevel.ToString() + ".bmp");
                                for (int y = 0; y < mipSize.Height; y++)
                                {
                                    for (int x = 0; x < mipSize.Width; x++)
                                    {
                                        Color color = qImage.GetPixel(x, y);
                                        int paletteIndex = Array.FindIndex(qImage.Palette.Entries, c => (c.R == color.R && c.G == color.G && c.B == color.B));
                                        mipData[(y * mipSize.Width) + x] = Convert.ToByte(paletteIndex);
                                    }
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
            using (FileStream fs = File.OpenWrite(opts.DestWAD))
            {
                wad.Write(fs);
            }
            Console.WriteLine("WAD successfully generated.");
        }

        static void HandleParseError(IEnumerable<Error> errs)
        {
            Console.Error.WriteLine("Usage: ./wad3-cli <SourceFolder> <DestWAD>");
        }

        static Bitmap ScaleImage(Bitmap image, Size size)
        {
            Bitmap newImage = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(newImage))
            {
                g.DrawImage(image, 0, 0, size.Width, size.Height);
            }
            return newImage;
        }
        static byte[] GetPaletteBytes(Bitmap image)
        {
            var length = 256 * 3;
            byte[] paletteData = new byte[length];
            for (int i = 0; i < length; i += 3)
            {
                int paletteIndex = i / 3;
                paletteData[i] = image.Palette.Entries[paletteIndex].R;
                paletteData[i + 1] = image.Palette.Entries[paletteIndex].G;
                paletteData[i + 2] = image.Palette.Entries[paletteIndex].B;
            }
            return paletteData;
        }
    }
}