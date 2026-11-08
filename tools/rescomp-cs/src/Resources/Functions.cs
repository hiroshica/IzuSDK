using System.Text;

namespace Isdk.Rescomp.Resources;

/// <summary>ALIGN: これより後ろの far な BIN データの番地をそろえる。</summary>
public sealed class Align : Resource
{
    public readonly int AlignValue;

    public Align(string id, int align) : base(id) { AlignValue = align; }

    public override int InternalHashCode() => AlignValue;
    public override bool InternalEquals(object obj) => obj is Align a && a.AlignValue == AlignValue;
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => 0;
    public override int TotalSize() => 0;

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        outS.Append("    .align  " + AlignValue + "\n");
        outS.Append("\n");
    }
}

/// <summary>UNGROUP: BIN データを種類ごとにまとめずに出力する。</summary>
public sealed class Ungroup : Resource
{
    public Ungroup(string id) : base(id) { }

    public override int InternalHashCode() => 1;
    public override bool InternalEquals(object obj) => obj is Ungroup;
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => 0;
    public override int TotalSize() => 0;
    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH) { }
}

/// <summary>NEAR: far な BIN データも near のセクションに置く。</summary>
public sealed class Near : Resource
{
    public Near(string id) : base(id) { }

    public override int InternalHashCode() => 0;
    public override bool InternalEquals(object obj) => obj is Near;
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => 0;
    public override int TotalSize() => 0;
    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH) { }
}
