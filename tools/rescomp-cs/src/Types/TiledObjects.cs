using System.Text;
using System.Xml;

namespace Isdk.Rescomp.Types;

/// <summary>IsuSDK 側のフィールドの型 (Java 版の SFieldDef.SGDKObjectType)。</summary>
public enum IsdkObjectType { U8, S8, U16, S16, U32, S32, F16, F32, BOOL, STRING, OBJECT }

/// <summary>Tiled 側のプロパティの型 (Java 版の TFieldDef.TiledObjectType)。</summary>
public enum TiledObjectType { INT, FLOAT, BOOL, STRING, COLOR, FILE, OBJECT, ENUM, LIST }

public static class ObjectTypes
{
    public static string Name(this IsdkObjectType t) => t.ToString().ToLowerInvariant();
    public static string Name(this TiledObjectType t) => t.ToString().ToLowerInvariant();

    public static bool TryParseIsdk(string s, out IsdkObjectType type)
    {
        var t = s.ToLowerInvariant();
        foreach (IsdkObjectType v in Enum.GetValues<IsdkObjectType>())
            if (t == v.Name()) { type = v; return true; }
        type = default;
        return false;
    }

    public static IsdkObjectType ParseIsdk(string s) =>
        TryParseIsdk(s, out var t) ? t : throw new ArgumentException("Error: invalid ISDK field type: " + s);

    public static TiledObjectType ParseTiled(string s)
    {
        var t = s.ToLowerInvariant();
        foreach (TiledObjectType v in Enum.GetValues<TiledObjectType>())
            if (t == v.Name()) return v;
        throw new ArgumentException("Error: invalid Tiled field type: " + s);
    }

    public static int Size(this IsdkObjectType t) => t switch
    {
        IsdkObjectType.F16 or IsdkObjectType.S16 or IsdkObjectType.U16 => 2,
        IsdkObjectType.F32 or IsdkObjectType.S32 or IsdkObjectType.U32 or IsdkObjectType.STRING or IsdkObjectType.OBJECT => 4,
        _ => 1,
    };

    public static bool Accept(this IsdkObjectType t, TiledObjectType type) => t switch
    {
        IsdkObjectType.U8 => type is TiledObjectType.INT or TiledObjectType.ENUM,
        IsdkObjectType.U16 => type is TiledObjectType.FLOAT or TiledObjectType.INT or TiledObjectType.ENUM,
        IsdkObjectType.U32 => type is TiledObjectType.FLOAT or TiledObjectType.INT or TiledObjectType.COLOR or TiledObjectType.ENUM,
        IsdkObjectType.S8 => type == TiledObjectType.INT,
        IsdkObjectType.S16 or IsdkObjectType.S32 or IsdkObjectType.F16 or IsdkObjectType.F32 => type is TiledObjectType.FLOAT or TiledObjectType.INT,
        IsdkObjectType.BOOL => type == TiledObjectType.BOOL,
        IsdkObjectType.STRING => type == TiledObjectType.STRING,
        IsdkObjectType.OBJECT => type == TiledObjectType.OBJECT,
        _ => false,
    };
}

/// <summary>出力するオブジェクトの 1 フィールド (Java 版の SField)。</summary>
public sealed class SField
{
    static int genId;

    public readonly string Name;
    public readonly IsdkObjectType Type;
    public readonly long? LongValue;
    readonly int internalId;
    public bool Padding;
    string value;

    public SField(string name, IsdkObjectType type, string value)
    {
        Name = name;
        Type = type;
        this.value = value;
        LongValue = ComputeLongValue();
        // 文字列のラベルを重ならないようにする番号
        internalId = genId++;
    }

    public bool IsString => Type == IsdkObjectType.STRING;
    public bool IsPointer => Type == IsdkObjectType.OBJECT;
    public bool IsIntData => !IsString && !IsPointer;

    long? ComputeLongValue()
    {
        if (!IsIntData)
            return null;
        if (!JavaCompat.TryParseDouble(value, out double v))
            return null;

        if (Type == IsdkObjectType.F16)
        {
            if (v > 511.4d || v < -512.4d)
                throw new Exception("Value " + v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " for field '" + Name + "' overflows the specified fix16 type");
            v *= Math.Pow(2, 6);
        }
        else if (Type == IsdkObjectType.F32)
        {
            if (v > 2097151.4d || v < -2097152.4d)
                throw new Exception("Value " + v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " for field '" + Name + "' overflows the specified fix32 type");
            v *= Math.Pow(2, 10);
        }

        long result = JavaCompat.Round(v);
        bool overflow = Type switch
        {
            IsdkObjectType.S8 => result > sbyte.MaxValue || result < sbyte.MinValue,
            IsdkObjectType.U8 => result > 255 || result < 0,
            IsdkObjectType.S16 => result > short.MaxValue || result < short.MinValue,
            IsdkObjectType.U16 => result > 65535 || result < 0,
            IsdkObjectType.S32 => result > int.MaxValue || result < int.MinValue,
            IsdkObjectType.U32 => result > 4294967295L || result < 0,
            _ => false,
        };
        if (overflow)
            throw new Exception("Value " + result + " for field '" + Name + "' overflows the specified " + Type.Name() + " type");
        return result;
    }

    public int TotalSize() => Size() + (IsString ? value.Length + 1 : 0);
    public int Size() => Type.Size() + (Padding ? 1 : 0);

    public override bool Equals(object obj) => obj is SField f && Type == f.Type && value == f.value && Padding == f.Padding;
    public override int GetHashCode() => (int)Type;

    public void OutPre(StringBuilder outS)
    {
        if (!IsString)
            return;
        outS.Append("    .align 2\n");
        outS.Append(Name + "_" + internalId + ":\n");
        outS.Append("    .asciz \"" + value + "\"\n");
        outS.Append("\n");
    }

    public void OutNonIntData(StringBuilder outS)
    {
        if (IsIntData)
            return;
        if (IsPointer)
            outS.Append("    dc.l    " + (string.IsNullOrEmpty(value) ? "0" : value) + "\t\t// " + Name + "\n");
        else
            outS.Append("    dc.l    " + Name + "_" + internalId + "\t\t// " + Name + "\n");
    }

    public void OutIntData(StringBuilder outS)
    {
        if (!IsIntData)
            return;
        string sVal = LongValue == null ? value : LongValue.Value.ToString();
        switch (Type)
        {
            case IsdkObjectType.F32:
            case IsdkObjectType.S32:
            case IsdkObjectType.U32:
                outS.Append("    dc.l    " + sVal + "\t\t// " + Name + "\n");
                break;
            case IsdkObjectType.F16:
            case IsdkObjectType.S16:
            case IsdkObjectType.U16:
                outS.Append("    dc.w    " + sVal + "\t\t// " + Name + "\n");
                break;
            case IsdkObjectType.S8:
            case IsdkObjectType.U8:
            case IsdkObjectType.BOOL:
                outS.Append("    dc.b    " + sVal + "\t\t// " + Name + "\n");
                break;
        }
        if (Padding)
            outS.Append("    dc.b    0\t\t// padding\n");
    }

    /// <summary>値が id と同じなら、参照先のラベル名に置き換える。</summary>
    public void ChangeValueToIfContainId(string newValue, int id)
    {
        int contained = JavaCompat.ParseInt(value, 0);
        if (contained == 0)
            return;
        if (contained == id)
            value = newValue;
    }
}

/// <summary>Tiled から読んだプロパティ (Java 版の TField)。</summary>
public sealed class TField
{
    public readonly string Name;
    public readonly TiledObjectType Type;
    readonly string value;

    public TField(string name, TiledObjectType type, string value)
    {
        Name = name;
        Type = type;
        this.value = value;
    }

    public SField ToSField(IsdkObjectType isdkType)
    {
        if (!isdkType.Accept(Type))
            throw new Exception("Error: cannot convert Tiled type '" + Type.Name() + " to ISDK '" + isdkType.Name() + "' type (not compatible)");
        string adjVal = value;
        // オブジェクトの参照: 0 は NULL。ほかは id のまま置いておき、あとで名前に直す
        if (isdkType == IsdkObjectType.OBJECT && JavaCompat.ParseInt(value, 0) == 0)
            adjVal = "";
        return new SField(Name, isdkType, adjVal);
    }
}

/// <summary>出力するオブジェクト 1 つ (Java 版の SObject)。</summary>
public sealed class SObject
{
    public readonly string File;
    public readonly int Id;
    public readonly string BaseName;
    public readonly string Type;
    public readonly double X;
    public readonly double Y;
    int off;
    public readonly List<SField> Fields = new();

    public SObject(string file, int id, string baseName, string type, double x, double y)
    {
        File = file;
        Id = id;
        BaseName = baseName;
        Type = type;
        X = x;
        Y = y;
    }

    public int GetFieldIndex(string fieldName) => Fields.FindIndex(f => f.Name == fieldName);

    public void UpdateObjectFieldsReferencesTo(SObject linked)
    {
        string reference = linked.BaseName + "_" + linked.Id;
        foreach (var f in Fields.Where(f => f.Type == IsdkObjectType.OBJECT).ToList())
            f.ChangeValueToIfContainId(reference, linked.Id);
    }

    public void AddField(SField field)
    {
        if (GetFieldIndex(field.Name) != -1)
            Console.WriteLine("Warning: Object '" + Name + "' already has a field named '" + field.Name + "', new field ignored...");

        // 奇数の位置に 2 バイト以上のものが来るなら、前のフィールドに詰め物をする
        if ((off & 1) != 0 && field.Type.Size() > 1)
        {
            Fields[^1].Padding = true;
            off++;
        }
        Fields.Add(field);
        off += field.Size();
    }

    public string Name => BaseName + "_" + Id;

    public long GetFieldLongValue(string fieldName)
    {
        int ind = GetFieldIndex(fieldName);
        if (ind == -1)
            return 0L;
        return Fields[ind].LongValue ?? 0L;
    }

    public int Size() => Fields.Sum(f => f.TotalSize());

    public void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        foreach (var f in Fields)
            f.OutPre(outS);
        outS.Append("    .align 2\n");
        outS.Append(Name + ":\n");
        foreach (var f in Fields)
        {
            if (f.IsIntData) f.OutIntData(outS);
            else f.OutNonIntData(outS);
        }
        outS.Append("\n");
    }
}

/// <summary>TMX のオブジェクトレイヤー (Java 版の TMX.TMXObjects)。</summary>
public sealed class TMXObjects
{
    public readonly string File;
    public readonly string LayerName;
    public readonly List<SObject> Objects = new();

    public TMXObjects(string baseId, string file, string layerName, string sortBy, List<(string Name, IsdkObjectType Type)> fieldDefs, string typeFilter)
    {
        if (!System.IO.File.Exists(file))
            throw new FileNotFoundException("TMX file '" + file + " not found !");

        File = file;
        LayerName = layerName;

        var mapElement = Xml.Load(file);
        if (mapElement.Name.ToLowerInvariant() != "map")
            throw new Exception("Expected map root node in TMX file: " + file + ", " + mapElement.Name + " found.");

        int tw = Xml.AttrInt(mapElement, "tilewidth", 0);
        int th = Xml.AttrInt(mapElement, "tileheight", 0);
        Tiled.CheckTileSize(tw, th, file);
        int tileSize = tw;
        int mapWidth = Xml.AttrInt(mapElement, "width", 0);
        if (mapWidth == 0)
            throw new Exception("Null map width in TMX file: " + file);

        var objectGroups = Xml.Elements(mapElement, "objectgroup");
        if (objectGroups.Count == 0)
            throw new Exception("No object layer found in TMX file: " + file);

        var group = Tiled.GetElementNamed(objectGroups, layerName)
            ?? throw new Exception("No object layer '" + layerName + "' found in TMX file: " + file);

        string baseObjectName = baseId + "_object";
        var tFields = new Dictionary<string, TField>();

        foreach (var objectElement in Xml.Elements(group, "object"))
        {
            // Tiled 1.8 までは type、それより後は class
            string objectType = Xml.Attr(objectElement, "type", "");
            if (string.IsNullOrEmpty(objectType))
                objectType = Xml.Attr(objectElement, "class", "");

            if (!string.IsNullOrEmpty(typeFilter) && objectType != typeFilter)
                continue;

            tFields.Clear();

            int id = Xml.AttrInt(objectElement, "id", 0);
            string objectName = Xml.Attr(objectElement, "name", "object_" + id);
            double x = Xml.AttrDouble(objectElement, "x", 0d);
            double y = Xml.AttrDouble(objectElement, "y", 0d);
            double w = Xml.AttrDouble(objectElement, "width", 0d);
            double h = Xml.AttrDouble(objectElement, "height", 0d);
            string visible = Xml.Attr(objectElement, "visible", "1");
            string templateName = Xml.Attr(objectElement, "template", "");

            if (!string.IsNullOrEmpty(templateName))
            {
                Console.WriteLine("Warning: template object '" + objectName + "' in layer '" + layerName + "' is not supported. Skipped.");
                continue;
            }

            string text = GetText(objectElement);

            foreach (var property in GetProperties(objectElement, new List<XmlElement>()))
            {
                string name = Xml.Attr(property, "name", "").ToLowerInvariant();
                string type = Xml.Attr(property, "type", "");
                string propertyType = Xml.Attr(property, "propertytype", "");
                string value = Xml.Attr(property, "value", "");
                bool isTrue = value.ToLowerInvariant() == "true";

                switch (name)
                {
                    case "exportid":
                        if (isTrue) AddField(objectName, tFields, new TField("id", TiledObjectType.INT, id.ToString()));
                        break;
                    case "exportname":
                        if (isTrue) AddField(objectName, tFields, new TField("name", TiledObjectType.STRING, objectName));
                        break;
                    case "exportposition":
                        if (isTrue)
                        {
                            AddField(objectName, tFields, new TField("x", TiledObjectType.FLOAT, D(x)));
                            AddField(objectName, tFields, new TField("y", TiledObjectType.FLOAT, D(y)));
                        }
                        break;
                    case "exportsize":
                        if (isTrue)
                        {
                            AddField(objectName, tFields, new TField("width", TiledObjectType.FLOAT, D(w)));
                            AddField(objectName, tFields, new TField("height", TiledObjectType.FLOAT, D(h)));
                        }
                        break;
                    case "exporttileindex":
                        if (isTrue)
                        {
                            int tileIndex = ((int)x / tileSize) + (((int)y / tileSize) * mapWidth);
                            AddField(objectName, tFields, new TField("tileindex", TiledObjectType.INT, D(tileIndex)));
                        }
                        break;
                    case "exportvisible":
                        if (isTrue) AddField(objectName, tFields, new TField("visible", TiledObjectType.BOOL, visible));
                        break;
                    case "exporttext":
                        if (isTrue) AddField(objectName, tFields, new TField("text", TiledObjectType.STRING, text));
                        break;
                    default:
                        if (string.IsNullOrEmpty(type))
                            AddField(objectName, tFields, new TField(name, string.IsNullOrEmpty(propertyType) ? TiledObjectType.STRING : TiledObjectType.ENUM, value));
                        else if (ObjectTypes.ParseTiled(type) == TiledObjectType.BOOL)
                            AddField(objectName, tFields, new TField(name, TiledObjectType.BOOL, isTrue ? "1" : "0"));
                        else
                            AddField(objectName, tFields, new TField(name, ObjectTypes.ParseTiled(type), value));
                        break;
                }
            }

            var obj = new SObject(file, id, baseObjectName, objectType, x, y);
            // フィールド定義の順に並べる
            foreach (var (fieldName, fieldType) in fieldDefs)
                if (tFields.TryGetValue(fieldName, out var field))
                    obj.AddField(field.ToSField(fieldType));

            Objects.Add(obj);
        }

        // Java の Collections.sort と同じく安定な並べ替え
        var sorted = Objects.Select((o, i) => (o, i)).OrderBy(p => p, Comparer<(SObject o, int i)>.Create((a, b) =>
        {
            int r = Compare(a.o, b.o, sortBy);
            return r != 0 ? r : a.i.CompareTo(b.i);
        })).Select(p => p.o).ToList();
        Objects.Clear();
        Objects.AddRange(sorted);
    }

    /// <summary>Java の Double.toString と同じ書き方 (整数でも ".0" が付く)。後で数値に読み直すだけなので、値が合えばよい。</summary>
    static string D(double v)
    {
        string s = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') ? s : s + ".0";
    }

    static int Compare(SObject o1, SObject o2, string sortBy)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            int r = o1.GetFieldLongValue("index").CompareTo(o2.GetFieldLongValue("index"));
            if (r == 0) r = o1.GetFieldLongValue("ind").CompareTo(o2.GetFieldLongValue("ind"));
            if (r == 0) r = o1.GetFieldLongValue("tileindex").CompareTo(o2.GetFieldLongValue("tileindex"));
            if (r == 0) r = o1.GetFieldLongValue("id").CompareTo(o2.GetFieldLongValue("id"));
            return r;
        }
        return o1.GetFieldLongValue(sortBy).CompareTo(o2.GetFieldLongValue(sortBy));
    }

    static void AddField(string objectName, Dictionary<string, TField> fields, TField field)
    {
        if (fields.ContainsKey(field.Name))
        {
            Console.WriteLine("Warning: Object '" + objectName + "' already has a field named '" + field.Name + "', new field ignored...");
            return;
        }
        fields[field.Name] = field;
    }

    static List<XmlElement> GetProperties(XmlElement objectElement, List<XmlElement> result)
    {
        var properties = Xml.Element(objectElement, "properties");
        if (properties == null)
            return result;
        foreach (var p in Xml.Elements(properties, "property"))
        {
            // class 型のプロパティは中身を取り出す
            if (Xml.Attr(p, "type", "").ToLowerInvariant() == "class")
                GetProperties(p, result);
            else
                result.Add(p);
        }
        return result;
    }

    static string GetText(XmlElement objectElement)
    {
        var text = Xml.Element(objectElement, "text");
        return text == null ? "" : Xml.Value(text, "");
    }

    /// <summary>同じ TMX から読んだオブジェクトどうしで、object 型のフィールドを相手のラベル名に直す。</summary>
    public static void ResolveObjectsReferences(List<Resource> resources)
    {
        var byFile = new Dictionary<string, List<SObject>>();
        var order = new List<string>();
        foreach (var r in resources.OfType<Resources.Objects>())
        {
            foreach (var obj in r.ObjectList)
            {
                if (!byFile.TryGetValue(obj.File, out var list))
                {
                    byFile[obj.File] = list = new List<SObject>();
                    order.Add(obj.File);
                }
                list.Add(obj);
            }
        }

        foreach (var file in order)
        {
            var list = byFile[file];
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    list[i].UpdateObjectFieldsReferencesTo(list[j]);
                    list[j].UpdateObjectFieldsReferencesTo(list[i]);
                }
            }
        }
    }
}
