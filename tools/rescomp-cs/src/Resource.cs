using System.Text;

namespace Sgdk.Rescomp;

/// <summary>
/// リソース (Java 版の sgdk.rescomp.Resource)。同じ中身のリソースは 1 つにまとめるので、
/// InternalHashCode と InternalEquals で中身を比べる。
/// </summary>
public abstract class Resource
{
    public readonly string Id;
    public bool Global = true;

    protected Resource(string id)
    {
        Id = id;
    }

    /// <summary>内部のリソースとして加える (同じ中身のものがあればそれを返す)。</summary>
    public static Resource AddInternalResource(Resource resource) => Compiler.AddResource(resource, true);

    /// <summary>グローバルなリソースとして加える。</summary>
    public static Resource AddResource(Resource resource) => Compiler.AddResource(resource, false);

    public static Resource FindResource(Resource resource) => Compiler.FindResource(resource);

    public abstract List<Resources.Bin> GetInternalBinResources();

    public abstract int InternalHashCode();

    public abstract bool InternalEquals(object obj);

    public sealed override int GetHashCode() => InternalHashCode();

    public sealed override bool Equals(object obj) =>
        obj is Resource res && GetHashCode() == res.GetHashCode() && InternalEquals(obj);

    /// <summary>このデータ構造だけの大きさ (参照先を数えない)。</summary>
    public abstract int ShallowSize();

    /// <summary>参照先も含めた大きさ。</summary>
    public abstract int TotalSize();

    /// <summary>
    /// 出力する。outB は直前に出力したバイナリ (LZ4W の圧縮で前のデータとして使う)、outS はアセンブリ、outH はヘッダ。
    /// </summary>
    public abstract void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH);
}

/// <summary>Java の ByteArrayOutputStream にあたる、追記だけのバイト列。</summary>
public sealed class BinaryStream
{
    readonly List<byte> data = new();

    public int Size => data.Count;
    public void Reset() => data.Clear();
    public void Write(int value) => data.Add((byte)value);
    public void Write(byte[] bytes) => data.AddRange(bytes);
    public byte[] ToArray() => data.ToArray();
}
