using Isdk.Rescomp.Resources;
using Isdk.Rescomp.Types;

namespace Isdk.Rescomp.Processors;

public sealed class ObjectsProcessor : IProcessor
{
    public string Id => "OBJECTS";

    /// <summary>"name:type;name:type" を、書いた順のままの一覧にする。</summary>
    public static List<(string Name, IsdkObjectType Type)> BuildFieldDefs(string fieldDefs)
    {
        var result = new List<(string, IsdkObjectType)>();
        foreach (var def in JavaCompat.SplitJava(fieldDefs, ";"))
        {
            var values = JavaCompat.SplitJava(def, ":");
            if (values.Length != 2)
                throw new Exception("Error: invalid field definition '" + def + "', name and type should be separated by ':' character");

            string fieldName = values[0].Trim().ToLowerInvariant();
            string fieldType = values[1].Trim().ToLowerInvariant();

            if (fieldName.Length == 0)
                throw new Exception("Error: invalid field definition '" + def + "', field name is empty");
            if (fieldType.Length == 0)
                throw new Exception("Error: invalid field definition '" + def + "', field type is empty");
            if (!ObjectTypes.TryParseIsdk(fieldType, out var t))
                throw new Exception("Error: invalid field definition '" + def + "', unknow field type");

            // LinkedHashMap.put と同じく、同じ名前は最初の位置のまま型だけ置き換える
            int idx = result.FindIndex(r => r.Item1 == fieldName);
            if (idx >= 0) result[idx] = (fieldName, t);
            else result.Add((fieldName, t));
        }

        if (result.Count == 0)
            throw new Exception("Error: field definition cannot be empty");
        return result;
    }

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 5)
        {
            Console.WriteLine("Wrong OBJECTS definition");
            Console.WriteLine("OBJECTS name tmx_file layer_id field_defs [sortby:<field>] [decl_type [type_filter]]");
            Console.WriteLine("name            name of the output array Objects structure");
            Console.WriteLine("tmx_file        path of the input TMX file (TMX Tiled file)");
            Console.WriteLine("layer_id        object group/layer name we want to extract object from.");
            Console.WriteLine("field_defs      name and type of fields we want to extract (also define the order of extraction) in <name>:<type> format.");
            Console.WriteLine("                    Field type can be any of the usual ISDK base type: u8, s8, u16, s16, u32, s32, f16, f32, bool, string, object");
            Console.WriteLine("                    Some examples of 'field_defs' declaration:");
            Console.WriteLine("                        \"type:u16;name:string;x:f32;y:f32;tileindex:u32\"");
            Console.WriteLine("                        \"type:u16;power:s8;visible:bool\"");
            Console.WriteLine("                    Not found fields are just ignored so you can extract different object types easily.");
            Console.WriteLine("sortby          optional, field name to sort objects by. e.g. \"sortby:x\" to sort objects by x position.");
            Console.WriteLine("decl_type       declaration type for objects (default is \"void\").");
            Console.WriteLine("                    By default object array is untyped (void) but you can specify your own type if you want (\"Object\", \"Entity\", ...)");
            Console.WriteLine("type_filter     define a type filter if we only want to extract Tiled objects of a specific type.");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        string layerName = fields[3];
        string fieldDefs = fields[4];

        int index = 5;
        string sortBy = "";
        if (index < fields.Length && fields[index].StartsWith("sortby:", StringComparison.Ordinal))
            sortBy = fields[index++].Substring(7);

        string declType = "";
        string typeFilter = "";
        if (index < fields.Length)
        {
            declType = fields[index++];
            if (index < fields.Length)
                typeFilter = fields[index++];
        }
        var defs = BuildFieldDefs(fieldDefs);

        Compiler.AddResourceFile(fileIn);

        var tmxObjects = new TMXObjects(id, fileIn, layerName, sortBy, defs, typeFilter);
        return new Objects(id, declType, tmxObjects.Objects);
    }
}
