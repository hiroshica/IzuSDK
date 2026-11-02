using System.Text;
using Sgdk.Rescomp.Imaging;

namespace Sgdk.Rescomp.Resources;

/// <summary>4bpp のビットマップ (Java 版の resource.Bitmap)。</summary>
public sealed class Bitmap : Resource
{
    public readonly int W;
    public readonly int H;
    public readonly Bin Bin;
    public readonly Palette Palette;
    readonly int hc;

    public Bitmap(string id, string imgFile, Compression compression) : base(id)
    {
        var info = ImageUtil.GetBasicInfo(imgFile);
        W = info.W;
        if ((W & 1) == 1)
            throw new ArgumentException("'" + imgFile + "' width is '" + W + ", even width (multiple of 2) required.");

        var data = ImageUtil.GetImageAs8bpp(imgFile, false, true);
        H = data.Length / W;

        int maxIndex = data.Length == 0 ? int.MinValue : data.Max(b => (int)b);
        if (maxIndex >= 16)
            throw new ArgumentException("'" + imgFile + "' uses color index >= 16, BITMAP resource requires image with a maximum of 16 colors (use 4bpp image instead if unsure)");

        data = ImageUtil.ConvertTo4bpp(data, 8);

        Bin = (Bin)AddInternalResource(new Bin(id + "_data", data, compression));
        Palette = (Palette)AddInternalResource(new Palette(id + "_palette", imgFile));

        hc = Bin.GetHashCode() ^ (W << 8) ^ (H << 16) ^ Palette.GetHashCode();
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) =>
        obj is Bitmap b && W == b.W && H == b.H && Palette.Equals(b.Palette) && Bin.Equals(b.Bin);
    public override int ShallowSize() => 2 + 2 + 2 + 4 + 4;
    public override int TotalSize() => Bin.TotalSize() + Palette.TotalSize() + ShallowSize();
    public override List<Bin> GetInternalBinResources() => new() { Bin };

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "Bitmap", Id, 2, Global);
        outS.Append("    dc.w    " + ((int)Bin.DoneCompression - 1) + "\n");
        outS.Append("    dc.w    " + W + ", " + H + "\n");
        outS.Append("    dc.l    " + Palette.Id + "\n");
        outS.Append("    dc.l    " + Bin.Id + "\n");
        outS.Append("\n");
    }
}
