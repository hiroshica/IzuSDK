using Sgdk.Rescomp.Resources;

namespace Sgdk.Rescomp.Processors;

public sealed class BitmapProcessor : IProcessor
{
    public string Id => "BITMAP";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong BITMAP definition");
            Console.WriteLine("BITMAP name \"file\" [compression]");
            Console.WriteLine("  name          Bitmap variable name");
            Console.WriteLine("  file          the image to convert to Bitmap structure (BMP or PNG image)");
            Console.WriteLine("  compression   compression type, accepted values:");
            Console.WriteLine("                   -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                    0 / NONE        = no compression (default)");
            Console.WriteLine("                    1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                    2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        var compression = fields.Length >= 4 ? Util.GetCompression(fields[3]) : Compression.NONE;

        Compiler.AddResourceFile(fileIn);
        return new Bitmap(id, fileIn, compression);
    }
}

public sealed class ImageProcessor : IProcessor
{
    public string Id => "IMAGE";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong IMAGE definition");
            Console.WriteLine("IMAGE name \"file\" [compression [map_opt [map_base]]]");
            Console.WriteLine("  name          Image variable name");
            Console.WriteLine("  file          path of the input image file (BMP or PNG image)");
            Console.WriteLine("  compression   compression type, accepted values:");
            Console.WriteLine("                   -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                    0 / NONE        = no compression (default)");
            Console.WriteLine("                    1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                    2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  map_opt       define the map optimisation level, accepted values:");
            Console.WriteLine("                    0 / NONE        = no optimisation (each tile is unique)");
            Console.WriteLine("                    1 / ALL         = find duplicate and flipped tile (default)");
            Console.WriteLine("                    2 / DUPLICATE   = find duplicate tile only");
            Console.WriteLine("  map_base      define the base tilemap value, useful to set a default priority, palette and base tile index offset.");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        var compression = fields.Length >= 4 ? Util.GetCompression(fields[3]) : Compression.NONE;
        var tileOpt = fields.Length >= 5 ? Util.GetTileOpt(fields[4]) : TileOptimization.ALL;
        int mapBase = fields.Length >= 6 ? JavaCompat.ParseInt(fields[5], 0) : 0;

        Compiler.AddResourceFile(fileIn);
        return new Image(id, fileIn, compression, tileOpt, mapBase);
    }
}

public sealed class TilesetProcessor : IProcessor
{
    public string Id => "TILESET";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong TILESET definition");
            Console.WriteLine("TILESET name \"file\" [compression [opt [ordering [export]]]]");
            Console.WriteLine("  name          Tileset variable name");
            Console.WriteLine("  file          path of the input file (BMP, PNG image file or TSX Tiled file)");
            Console.WriteLine("  compression   compression type, accepted values:");
            Console.WriteLine("                   -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                    0 / NONE        = no compression (default)");
            Console.WriteLine("                    1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                    2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  opt           define the optimisation level, accepted values:");
            Console.WriteLine("                    0 / NONE        = no optimisation, each tile is unique (default for TSX file)");
            Console.WriteLine("                    1 / ALL         = ignore duplicated and flipped tile (default for image file)");
            Console.WriteLine("                    2 / DUPLICATE   = ignore duplicated tile only");
            Console.WriteLine("  ordering      define the tile process order, accepted values:");
            Console.WriteLine("                    ROW             = process per row (default)");
            Console.WriteLine("                    COLUMN          = process per column");
            Console.WriteLine("  export        enable the tileset PNG export, accept values:");
            Console.WriteLine("                    0 / FALSE       = no PNG export (default)");
            Console.WriteLine("                    1 / TRUE        = optimized tileset is export to PNG format '<file>-tileset-export.png'");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        bool tsx = JavaCompat.GetFileExtension(fileIn, false).ToLowerInvariant() == "tsx";

        var compression = fields.Length >= 4 ? Util.GetCompression(fields[3]) : Compression.NONE;
        // TSX は ALL に固定
        var opt = (!tsx && fields.Length >= 5) ? Util.GetTileOpt(fields[4]) : TileOptimization.ALL;
        var order = fields.Length >= 6 ? Util.GetTileOrdering(fields[5]) : TileOrdering.ROW;
        bool export = fields.Length >= 7 && JavaCompat.ParseBoolean(fields[6], false);

        Compiler.AddResourceFile(fileIn);

        if (tsx)
            throw new NotPortedException("TSX file is not supported yet by the C# rescomp: '" + fileIn + "'");

        return Tileset.GetTileset(id, fileIn, compression, opt, false, false, order, export);
    }
}

public sealed class TilemapProcessor : IProcessor
{
    public string Id => "TILEMAP";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong TILEMAP definition");
            Console.WriteLine("TILEMAP name \"img_file\" tileset_id [compression [map_opt [map_base [ordering]]]]");
            Console.WriteLine("  name          Tilemap variable name");
            Console.WriteLine("  file          path of the input image file (BMP or PNG image file)");
            Console.WriteLine("  tileset_id    base tileset resource to use (allow to share tileset along several maps)");
            Console.WriteLine("  compression   compression type, accepted values:");
            Console.WriteLine("                   -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                    0 / NONE        = no compression (default)");
            Console.WriteLine("                    1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                    2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  map_opt       define the map optimisation level, accepted values:");
            Console.WriteLine("                    0 / NONE        = no optimisation, each tile is unique");
            Console.WriteLine("                    1 / ALL         = find duplicate and flipped tile (default)");
            Console.WriteLine("                    2 / DUPLICATE   = find duplicate tile only");
            Console.WriteLine("  map_base      define the base tilemap value, useful to set a default priority, palette and base tile index offset");
            Console.WriteLine("  ordering      define the tilemap process order, accepted values:");
            Console.WriteLine("                    ROW             = process per row (default)");
            Console.WriteLine("                    COLUMN          = process per column");
            Console.WriteLine();
            Console.WriteLine("TILEMAP name \"tmx_file\" \"layer_id\" [ts_compression [map_compression [map_base [ordering]]]]");
            Console.WriteLine("  name              Tilemap variable name");
            Console.WriteLine("  tmx_file          path of the input TMX file (TMX Tiled file)");
            Console.WriteLine("  layer_id          layer name we want to extract map data from.");
            Console.WriteLine("  ts_compression    compression type for tileset, accepted values:");
            Console.WriteLine("                       -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                        0 / NONE        = no compression (default)");
            Console.WriteLine("                        1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                        2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  map_compression   compression type for map (same accepted values then 'ts_compression')");
            Console.WriteLine("  map_base          define the base tilemap value, useful to set a default priority, palette and base tile index offset.");
            Console.WriteLine("  ordering          define the tilemap process order, accepted values:");
            Console.WriteLine("                        ROW             = process per row (default)");
            Console.WriteLine("                        COLUMN          = process per column");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);

        Compiler.AddResourceFile(fileIn);

        if (JavaCompat.GetFileExtension(fileIn, false).ToLowerInvariant() == "tmx")
            throw new NotPortedException("TMX file is not supported yet by the C# rescomp: '" + fileIn + "'");

        var compression = fields.Length >= 5 ? Util.GetCompression(fields[4]) : Compression.NONE;
        var tileOpt = fields.Length >= 6 ? Util.GetTileOpt(fields[5]) : TileOptimization.ALL;
        int mapBase = fields.Length >= 7 ? JavaCompat.ParseInt(fields[6], 0) : 0;
        var order = fields.Length >= 8 ? Util.GetTileOrdering(fields[7]) : TileOrdering.ROW;

        var tileset = Compiler.GetResourceById(fields[3]) as Tileset
            ?? throw new ArgumentException("TILEMAP resource definition error: Tileset '" + fields[3] + "' not found !");

        return Tilemap.GetTilemap(id, tileset, mapBase, fileIn, tileOpt, compression, order);
    }
}
