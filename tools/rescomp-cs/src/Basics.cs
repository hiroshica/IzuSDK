namespace Isdk.Rescomp;

public enum SoundDriver { PCM, DPCM2, PCM4 }

/// <summary>圧縮の種類 (並びは Java 版と同じ。Util.Pack がこの順番に頼る)。</summary>
public enum Compression { AUTO, NONE, APLIB, LZ4W }

public enum TileOptimization { NONE, ALL, DUPLICATE_ONLY }

public enum TileOrdering { ROW, COLUMN }

public enum CollisionType { NONE, CIRCLE, BOX }

public sealed class PackedData
{
    public readonly byte[] Data;
    public readonly Compression Compression;

    public PackedData(byte[] data, Compression compression)
    {
        Data = data;
        Compression = compression;
    }
}

/// <summary>タイルの一致の種類 (Java 版の TileEquality)。</summary>
public enum TileEquality { NONE, EQUAL, VFLIP, HFLIP, HVFLIP }

public static class TileEqualityExt
{
    public static bool HFlip(this TileEquality e) => e == TileEquality.HFLIP || e == TileEquality.HVFLIP;
    public static bool VFlip(this TileEquality e) => e == TileEquality.VFLIP || e == TileEquality.HVFLIP;
}

/// <summary>まだ C# に移していない機能に当たったときの例外 (Program は終了コード 3 を返す)。</summary>
public sealed class NotPortedException : Exception
{
    public NotPortedException(string message) : base(message) { }
}
