using Sgdk.Rescomp.Resources;

namespace Sgdk.Rescomp.Processors;

public sealed class MapProcessor : IProcessor
{
    public string Id => "MAP";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 4)
        {
            Console.WriteLine("Wrong MAP definition");
            Console.WriteLine("MAP name \"img_file\" tileset_id [compression [map_base]]");
            Console.WriteLine("  name          Map variable name");
            Console.WriteLine("  img_file      path of the input image file (BMP or PNG image file)");
            Console.WriteLine("  tileset_id    base tileset resource to use (allow to share tileset along several maps)");
            Console.WriteLine("  compression   compression type, accepted values:");
            Console.WriteLine("                   -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                    0 / NONE        = no compression (default)");
            Console.WriteLine("                    1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                    2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  map_base      define the base tilemap value, useful to set a default priority, palette and base tile index offset");
            Console.WriteLine("                    using a base tile index offset (static tile allocation) allow to use faster MAP decoding function internally.");
            Console.WriteLine();
            Console.WriteLine("MAP name \"tmx_file\" \"layer_id\" [ts_compression [map_compression [map_base [ordering]]]]");
            Console.WriteLine("  name              Map variable name");
            Console.WriteLine("  tmx_file          path of the input TMX file (TMX Tiled file)");
            Console.WriteLine("  layer_id          layer name we want to extract map data from.");
            Console.WriteLine("  ts_compression    compression type for tileset, accepted values:");
            Console.WriteLine("                       -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                        0 / NONE        = no compression (default)");
            Console.WriteLine("                        1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                        2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  map_compression   compression type for map (same accepted values then 'ts_compression')");
            Console.WriteLine("  map_base          define the base tilemap value, useful to set a default priority, palette and base tile index offset");
            Console.WriteLine("                        using a base tile index offset (static tile allocation) allow to use faster MAP decoding function internally.");
            Console.WriteLine("  ordering          define the map process order, accepted values:");
            Console.WriteLine("                        ROW             = process per row (default)");
            Console.WriteLine("                        COLUMN          = process per column");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        Compiler.AddResourceFile(fileIn);

        if (JavaCompat.GetFileExtension(fileIn, false).ToLowerInvariant() == "tmx")
        {
            string layerName = fields[3];
            var tsComp = fields.Length >= 5 ? Util.GetCompression(fields[4]) : Compression.NONE;
            var mapComp = fields.Length >= 6 ? Util.GetCompression(fields[5]) : Compression.NONE;
            int tmxMapBase = fields.Length >= 7 ? JavaCompat.ParseInt(fields[6], 0) : 0;
            var order = fields.Length >= 8 ? Util.GetTileOrdering(fields[7]) : TileOrdering.ROW;

            var tmxMap = new Types.TMXMap(fileIn, layerName);
            return new Map(id, tmxMap.GetMapImage(), tmxMap.W * tmxMap.TileSize, tmxMap.H * tmxMap.TileSize, tmxMapBase, 2,
                tmxMap.GetTilesets(id, tsComp, false, order), mapComp, true);
        }

        var tileset = Compiler.GetResourceById(fields[3]) as Tileset
            ?? throw new ArgumentException("MAP resource definition error: Tileset '" + fields[3] + "' not found !");

        var compression = fields.Length >= 5 ? Util.GetCompression(fields[4]) : Compression.NONE;
        int mapBase = fields.Length >= 6 ? JavaCompat.ParseInt(fields[5], 0) : 0;

        return Map.GetMap(id, fileIn, mapBase, 2, new List<Tileset> { tileset }, compression, true);
    }
}
