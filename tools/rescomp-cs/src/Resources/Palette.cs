using System.Text;
using Sgdk.Rescomp.Imaging;

namespace Sgdk.Rescomp.Resources;

/// <summary>パレット (Java 版の resource.Palette)。</summary>
public sealed class Palette : Resource
{
    public readonly Bin Bin;
    readonly int hc;

    public Palette(string id, string fileIn, int startIndex, int maxSize, bool align16) : base(id)
    {
        string file = fileIn;
        short[] palette;

        if (JavaCompat.GetFileExtension(file, false).Equals("pal", StringComparison.OrdinalIgnoreCase))
            palette = ImageUtil.GetRGBA4444PaletteFromPALFile(file, 0x0EEE);
        else
        {
            if (JavaCompat.GetFileExtension(file, false).Equals("tsx", StringComparison.OrdinalIgnoreCase))
                file = Types.Tiled.GetTSXTilesetPath(file);

            var info = ImageUtil.GetBasicInfo(file);
            if (info.Bpp > 8)
            {
                palette = ImageUtil.GetRGBA4444PaletteFromTiles(file, 0x0EEE);
                if (palette == null)
                    throw new ArgumentException("RGB image '" + file + "' does not contains palette data (see 'Important note about image format' in the rescomp.txt file");
            }
            else
                palette = ImageUtil.GetRGBA4444PaletteFromIndColImage(file, 0x0EEE);
        }

        int adjMaxSize = align16 ? (maxSize + 15) & 0xF0 : maxSize;
        if (palette.Length > adjMaxSize)
            Array.Resize(ref palette, adjMaxSize);

        // パレットは圧縮しない
        Bin = (Bin)AddInternalResource(new Bin(id + "_data", palette, Compression.NONE, false));
        hc = Bin.GetHashCode();
    }

    public Palette(string id, string file, int maxSize, bool align16) : this(id, file, 0, maxSize, true) { }

    public Palette(string id, string file) : this(id, file, 16, true) { }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) => obj is Palette p && Bin.Equals(p.Bin);
    public override List<Bin> GetInternalBinResources() => new() { Bin };
    public override int ShallowSize() => 2 + 4;
    public override int TotalSize() => Bin.TotalSize() + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "Palette", Id, 2, Global);
        outS.Append("    dc.w    " + (Bin.Data.Length / 2) + "\n");
        outS.Append("    dc.l    " + Bin.Id + "\n");
        outS.Append("\n");
    }
}
