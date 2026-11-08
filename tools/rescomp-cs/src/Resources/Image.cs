using System.Text;
using Isdk.Rescomp.Imaging;

namespace Isdk.Rescomp.Resources;

/// <summary>タイルセット + タイルマップ + パレット (Java 版の resource.Image)。</summary>
public sealed class Image : Resource
{
    public readonly Tileset Tileset;
    public readonly Tilemap Tilemap;
    public readonly Palette Palette;
    readonly int hc;

    public Image(string id, string imgFile, Compression compression, TileOptimization tileOpt, int mapBase) : base(id)
    {
        var image = ImageUtil.GetImageAs8bpp(imgFile, true, true)
            ?? throw new ArgumentException("RGB image '" + imgFile + "' does not contains palette data (see 'Important note about image format' in the rescomp.txt file");

        foreach (var d in image)
            if ((d & 0x40) != 0)
                throw new ArgumentException("'" + imgFile + "' has color index in [64..127] range, IMAGE resource requires image with a maximum of 64 colors");

        var info = ImageUtil.GetBasicInfo(imgFile);
        int w = info.W;
        int h = image.Length / w;
        int wt = w / 8;
        int ht = h / 8;

        Tileset = (Tileset)AddInternalResource(new Tileset(id + "_tileset", image, w, h, 0, 0, wt, ht, tileOpt, compression, false, false, TileOrdering.ROW));
        Tilemap = (Tilemap)AddInternalResource(Tilemap.GetTilemap(id + "_tilemap", Tileset, mapBase, image, wt, ht, tileOpt, compression, TileOrdering.ROW));
        Palette = (Palette)AddInternalResource(new Palette(id + "_palette", imgFile, 64, true));

        hc = Tileset.GetHashCode() ^ Tilemap.GetHashCode() ^ Palette.GetHashCode();
    }

    public int Width => Tilemap.W * 8;
    public int Height => Tilemap.H * 8;

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) =>
        obj is Image i && Palette.Equals(i.Palette) && Tilemap.Equals(i.Tilemap) && Tileset.Equals(i.Tileset);
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => 4 + 4 + 4;
    public override int TotalSize() => Palette.TotalSize() + Tileset.TotalSize() + Tilemap.TotalSize() + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "Image", Id, 2, Global);
        outS.Append("    dc.l    " + Palette.Id + "\n");
        outS.Append("    dc.l    " + Tileset.Id + "\n");
        outS.Append("    dc.l    " + Tilemap.Id + "\n");
        outS.Append("\n");
    }
}
