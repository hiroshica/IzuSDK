namespace Sgdk.Rescomp;

public enum SoundDriver { PCM, DPCM2, PCM4, XGM, XGM2 }

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
