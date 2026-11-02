using System.Text;

namespace Sgdk.Rescomp.Resources;

/// <summary>バイナリデータ (Java 版の resource.Bin)。ほかのリソースのデータもこれで出力する。</summary>
public class Bin : Resource
{
    public readonly byte[] Data;
    public readonly int AlignValue;
    public readonly Compression WantedCompression;
    public PackedData PackedData;
    public Compression DoneCompression;
    public readonly bool Far;
    public readonly bool Embedded;

    readonly int hc;

    public Bin(string id, byte[] data, int align, int sizeAlign, int fill, Compression compression, bool far, bool embedded) : base(id)
    {
        Data = sizeAlign > 0 ? Util.SizeAlign(data, sizeAlign, (byte)fill) : data;
        AlignValue = align;
        WantedCompression = compression;
        DoneCompression = Compression.NONE;
        Far = far;
        Embedded = embedded;
        // Java 版と同じく、そろえる前のデータで計算する (Enum の hashCode は同じ種類なら同じなので省く)
        hc = JavaCompat.HashCode(data) ^ (align << 16) ^ (int)compression;
    }

    public Bin(string id, byte[] data, int align, int sizeAlign, int fill, Compression compression, bool far)
        : this(id, data, align, sizeAlign, fill, compression, far, true) { }

    public Bin(string id, byte[] data, int align, int sizeAlign, int fill, Compression compression)
        : this(id, data, align, sizeAlign, fill, compression, true) { }

    public Bin(string id, byte[] data, int align, int sizeAlign, int fill)
        : this(id, data, align, sizeAlign, fill, Compression.NONE) { }

    public Bin(string id, byte[] data, int align, Compression compression)
        : this(id, data, align, 0, 0, compression) { }

    public Bin(string id, byte[] data, Compression compression)
        : this(id, data, 2, 0, 0, compression) { }

    public Bin(string id, short[] data, Compression compression, bool far)
        : this(id, JavaCompat.ShortToByte(data), 2, 0, 0, compression, far) { }

    public Bin(string id, short[] data, Compression compression)
        : this(id, JavaCompat.ShortToByte(data), 2, 0, 0, compression, true) { }

    public Bin(string id, int[] data, Compression compression)
        : this(id, JavaCompat.IntToByte(data), 2, 0, 0, compression) { }

    public override int InternalHashCode() => hc;

    public override bool InternalEquals(object obj) =>
        obj is Bin bin && AlignValue == bin.AlignValue && WantedCompression == bin.WantedCompression && Data.AsSpan().SequenceEqual(bin.Data);

    public override List<Bin> GetInternalBinResources() => new();

    public override int ShallowSize() => PackedData != null ? PackedData.Data.Length : Data.Length;

    public override int TotalSize() => ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        // 圧縮の前にそろえる (LZ4W は直前のデータを使うことがある)
        Util.Align(outB, AlignValue);

        // 埋め込みでないものは指定の圧縮を必ず使う
        PackedData = Util.Pack(Data, WantedCompression, outB, !Embedded);
        DoneCompression = PackedData.Compression;

        int baseSize = Data.Length;
        int packedSize = PackedData.Data.Length;

        if (WantedCompression != Compression.NONE)
        {
            Console.Write("'" + Id + "' ");
            switch (DoneCompression)
            {
                case Compression.NONE: Console.WriteLine("not packed (size = " + baseSize + ")"); break;
                case Compression.APLIB: Console.Write("packed with APLIB, "); break;
                case Compression.LZ4W: Console.Write("packed with LZ4W, "); break;
                default: Console.Write("packed with UNKNOW, "); break;
            }
            if (DoneCompression != Compression.NONE)
                Console.WriteLine("size = " + packedSize + " (" + JavaCompat.Round((packedSize * 100f) / baseSize) + "% - origin size = " + baseSize + ")");
        }

        Util.OutB(outB, PackedData.Data);

        Util.DeclArray(outS, outH, "u8", Id, PackedData.Data.Length, AlignValue, Global);
        Util.OutS(outS, PackedData.Data, 1);
        Util.DeclArrayEnd(outS, outH, "u8", Id, PackedData.Data.Length, AlignValue, Global);
        outS.Append("\n");
    }
}
