using System.Text;
using Sgdk.Rescomp.Types;

namespace Sgdk.Rescomp.Resources;

/// <summary>TMX のオブジェクトの配列 (Java 版の resource.Objects)。</summary>
public sealed class Objects : Resource
{
    // Java 版は SObject の hashCode が物ごとに違うので、同じ中身でもまとめられない。それをまねる
    static int uniqueHash;

    readonly string type;
    public readonly List<SObject> ObjectList;
    readonly int hc;

    public Objects(string id, string type, List<SObject> objects) : base(id)
    {
        this.type = type;
        ObjectList = objects;
        hc = ++uniqueHash;
    }

    public int Size => ObjectList.Count;

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) => ReferenceEquals(this, obj);
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => ObjectList.Count * 4;
    public override int TotalSize() => ObjectList.Sum(o => o.Size()) + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        foreach (var o in ObjectList)
            o.Out(outB, outS, outH);

        Util.DeclArray(outS, outH, (string.IsNullOrEmpty(type) ? "void" : type) + "*", Id, ObjectList.Count, 2, Global);
        foreach (var o in ObjectList)
            outS.Append("    dc.l    " + o.Name + "\n");
        outS.Append("\n");
    }
}
