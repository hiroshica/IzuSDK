using Isdk.Rescomp.Resources;

namespace Isdk.Rescomp.Processors;

public sealed class AlignProcessor : IProcessor
{
    public string Id => "ALIGN";

    public Resource Execute(string[] fields)
    {
        // 既定は 512KB (バンクの大きさ)
        int alignment = 524288;
        if (fields.Length > 1)
            alignment = JavaCompat.ParseInt(fields[1], alignment);
        return new Align("align", alignment);
    }
}

public sealed class UngroupProcessor : IProcessor
{
    public string Id => "UNGROUP";
    public Resource Execute(string[] fields) => new Ungroup("ungroup");
}

public sealed class NearProcessor : IProcessor
{
    public string Id => "NEAR";
    public Resource Execute(string[] fields) => new Near("near");
}

public sealed class BinProcessor : IProcessor
{
    public string Id => "BIN";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong BIN definition");
            Console.WriteLine("BIN name file [align [size_align [fill [compression [far]]]]]");
            Console.WriteLine("  name          BIN data variable name");
            Console.WriteLine("  file          path of the data file to convert to binary data array");
            Console.WriteLine("  align         memory address alignment for generated data array (default is 2)");
            Console.WriteLine("  size_align    size alignment for the generated data array (default is 2)");
            Console.WriteLine("  fill          fill value for the size alignment (default is 0)");
            Console.WriteLine("  compression   compression type, accepted values:");
            Console.WriteLine("                    0 / NONE        = no compression (default)");
            Console.WriteLine("                    1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                    2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  far           'far' binary data flag to put it at the end of the ROM (useful for bank switch, default = TRUE)");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        int align = fields.Length >= 4 ? JavaCompat.ParseInt(fields[3], 2) : 2;
        int salign = fields.Length >= 5 ? JavaCompat.ParseInt(fields[4], 2) : 2;
        int fill = fields.Length >= 6 ? JavaCompat.ParseInt(fields[5], 0) : 0;
        var compression = fields.Length >= 7 ? Util.GetCompression(fields[6]) : Compression.NONE;
        if (compression == Compression.AUTO)
            throw new ArgumentException("Cannot use AUTO compression on BIN resource !");
        bool far = fields.Length >= 8 ? JavaCompat.ParseBoolean(fields[7], true) : true;

        var data = JavaCompat.Load(fileIn);
        if (data == null)
            return null;

        Compiler.AddResourceFile(fileIn);
        return new Bin(id, data, align, salign, fill, compression, far, false);
    }
}

public sealed class PaletteProcessor : IProcessor
{
    public string Id => "PALETTE";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong PALETTE definition");
            Console.WriteLine("PALETTE name file");
            Console.WriteLine("  name       Palette variable name");
            Console.WriteLine("  file       path of the .pal or image file to convert to Palette structure (PAL file, BMP or PNG image file)");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        Compiler.AddResourceFile(fileIn);
        return new Palette(id, fileIn, 64, true);
    }
}
