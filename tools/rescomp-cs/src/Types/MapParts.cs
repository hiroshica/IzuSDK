namespace Isdk.Rescomp.Types;

/// <summary>メタタイル (NxN タイルのかたまり)。Java 版の type.Metatile。</summary>
public sealed class Metatile
{
    public readonly int Size;
    public readonly short[] Data;
    int hc;

    public Metatile(int size = 2)
    {
        Size = size;
        Data = new short[size * size];
    }

    public void Set(int index, short tileAttr) => Data[index] = tileAttr;

    public void UpdateInternals() => hc = Size ^ MapHash.Of(Data);

    public override int GetHashCode() => hc;

    public override bool Equals(object obj) =>
        obj is Metatile mt && mt.hc == hc && mt.Size == Size && mt.Data.AsSpan().SequenceEqual(Data);
}

/// <summary>マップブロック (8x8 メタタイル = 128x128 ピクセル)。Java 版の type.MapBlock。</summary>
public sealed class MapBlock
{
    public readonly short[] Data = new short[8 * 8];
    int hc;

    public void Set(int index, short attr) => Data[index] = attr;

    public void ComputeHashCode() => hc = MapHash.Of(Data);

    public override int GetHashCode() => hc;

    public override bool Equals(object obj) =>
        obj is MapBlock mb && mb.hc == hc && mb.Data.AsSpan().SequenceEqual(Data);
}

static class MapHash
{
    /// <summary>Java の Arrays.hashCode(short[]) と同じ。</summary>
    public static int Of(short[] a)
    {
        int h = 1;
        foreach (var s in a)
            h = unchecked(31 * h + s);
        return h;
    }
}
