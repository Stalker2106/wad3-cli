using System;
using CommandLine;
using Sledge.Formats.Texture.Wad;
using Sledge.Formats.Texture.Wad.Lumps;
using Sledge.Formats.Id;

namespace Wad3CLI
{
    class Program
    {
        public class Options
        {
            [Value(0, MetaName = "SourceFolder", HelpText = "Source folder that will be walked recursively to find all images to pack in WAD")]
            public string? SourceFolder { get; set; }
            [Value(1, MetaName = "DestWAD", HelpText = "Destination path for the generated WAD.")]
            public string? DestWAD { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
            .WithParsed(Run)
            .WithNotParsed(HandleParseError);
        }

        static void Run(Options opts)
        {
            WadFile wad = new WadFile(Sledge.Formats.Texture.Wad.Version.Wad3);

            // Fill
            var files = Directory.EnumerateFiles(opts.SourceFolder, "*.*", SearchOption.AllDirectories)
            .Where(s => s.EndsWith(".png"));
            Palette palette = new Palette();
            for (file in files) {
                ImageLump imgLump = new ImageLump();
                Png image = Png.Open(stream);
                // Set properties
                imgLump.Width = image.Width;
                imgLump.Height = image.Height;
                // Load image
                byte[] imageData = new byte[image.Width * image.Height];
                for (int y = 0; y < image.Height; y++) {
                    for (int x = 0; x < image.Width; x++) {
                        int paletteIndex = palette.GetOrAddColorIndex(image.GetPixel(x, y));
                        imageData[(y * image.Width) + x] = paletteIndex;
                    }
                }
                imgLump.ImageData = imageData;
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
    }
}