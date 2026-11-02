using System.Xml;
using Sgdk.Rescomp.Resources;

namespace Sgdk.Rescomp.Types;

/// <summary>Tiled の XML を読むための小物 (Java 版の XMLUtil の使っている分だけ)。</summary>
static class Xml
{
    public static XmlElement Load(string file)
    {
        var doc = new XmlDocument { XmlResolver = null };
        using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        doc.Load(reader);
        return doc.DocumentElement;
    }

    public static List<XmlElement> Elements(XmlNode node, string name) =>
        node.ChildNodes.OfType<XmlElement>().Where(e => e.Name == name).ToList();

    public static XmlElement Element(XmlNode node, string name) =>
        node?.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.Name == name);

    public static string Attr(XmlElement e, string name, string def) =>
        e?.GetAttributeNode(name) is XmlAttribute a ? a.Value : def;

    public static int AttrInt(XmlElement e, string name, int def) => JavaCompat.ParseInt(Attr(e, name, ""), def);

    public static double AttrDouble(XmlElement e, string name, double def) =>
        JavaCompat.TryParseDouble(Attr(e, name, ""), out var v) ? v : def;

    /// <summary>最初の子ノードの値 (Java の XMLUtil.getFirstValue)。</summary>
    public static string FirstValue(XmlElement e, string def)
    {
        if (e == null) return def;
        var child = e.FirstChild;
        return child != null ? child.Value : def;
    }

    /// <summary>子ノードのテキストをつないだもの (Java の XMLUtil.getValue)。</summary>
    public static string Value(XmlElement e, string def) => FirstValue(e, def);
}

/// <summary>Tiled のタイルセット (TSX)。Java 版の TSX.TSXTileset。</summary>
public sealed class TSXTileset : IComparable<TSXTileset>
{
    public readonly string File;
    public readonly int StartTileIndex;
    public int TileSize;
    public int ImageTileWidth;
    public int ImageTileHeigt;
    public int NumTile;
    public int TransparentColor;
    public string TilesetImagePath;

    public TSXTileset(string tmxFile, int startInd, XmlElement tilesetElement)
    {
        File = tmxFile;
        StartTileIndex = startInd;
        Load(tilesetElement);
    }

    public TSXTileset(string tsxFile, int startInd)
    {
        if (!System.IO.File.Exists(tsxFile))
            throw new FileNotFoundException("TSX file '" + tsxFile + " not found !");
        File = tsxFile;
        StartTileIndex = startInd;
        Load(Xml.Load(tsxFile));
    }

    void Load(XmlElement tilesetElement)
    {
        if (tilesetElement.Name.ToLowerInvariant() != "tileset")
            throw new Exception("Expected tileset root node in TSX file: " + File + ", " + tilesetElement.Name + " found.");

        int tw = Xml.AttrInt(tilesetElement, "tilewidth", 0);
        int th = Xml.AttrInt(tilesetElement, "tileheight", 0);
        if (tw != th)
            throw new Exception("Non square tile not supported (" + tw + " x " + th + ") in TSX file: " + File);
        if ((tw & 7) != 0)
            throw new Exception("Unsuported tile size (should be a multiple of 8) in TSX file: " + File);
        if (tw < 8 || tw > 32)
            throw new Exception(tw + " x " + th + " tile size not supported (only 8x8 to 32x32 allowed) in TSX file: " + File);

        TileSize = tw;

        int w = Xml.AttrInt(tilesetElement, "columns", 0);
        if (w == 0)
            throw new Exception("Tileset 'columns' information not found in TSX file: " + File);

        var imageElement = Xml.Element(tilesetElement, "image") ?? throw new Exception("Cannot find image XML node in TSX file: " + File);

        if (Xml.AttrInt(imageElement, "width", 0) != w * TileSize)
            throw new Exception("Image width (" + Xml.AttrInt(imageElement, "width", 0) + ") do not match tileset size (" + (w * TileSize) + ") in TSX file: " + File);

        ImageTileWidth = w;
        ImageTileHeigt = Xml.AttrInt(imageElement, "height", 0) / TileSize;
        NumTile = ImageTileWidth * ImageTileHeigt;
        TransparentColor = Xml.AttrInt(imageElement, "trans", 0);
        TilesetImagePath = Xml.Attr(imageElement, "source", "");
    }

    public bool ContainsTile(int tileInd) => tileInd >= StartTileIndex && tileInd < StartTileIndex + NumTile;

    public string TilesetPath => Tiled.GetAdjustedPath(TilesetImagePath, File);

    public Tileset GetTileset(string id, Compression compression, bool temp, TileOrdering order) =>
        // TSX のタイルセットには、なければ空白のタイルを足す (Tiled は数えない)
        Tileset.GetTileset(id, TilesetPath, compression, TileOptimization.ALL, true, temp, order, false);

    public byte[] GetTilesetImage8bpp(bool cropPalette) =>
        Imaging.ImageUtil.GetImageAs8bpp(TilesetPath, true, cropPalette)
            ?? throw new ArgumentException("RGB image '" + TilesetPath + "' does not contains palette data (see 'Important note about image format' in the rescomp.txt file");

    public override int GetHashCode() =>
        JavaCompat.HashCode(TilesetPath) ^ (NumTile << 16) ^ StartTileIndex ^ TileSize ^ ImageTileHeigt ^ ImageTileWidth;

    public override bool Equals(object obj) =>
        obj is TSXTileset t && t.TilesetPath == TilesetPath && t.NumTile == NumTile && t.StartTileIndex == StartTileIndex
            && t.TileSize == TileSize && t.ImageTileHeigt == ImageTileHeigt && t.ImageTileWidth == ImageTileWidth;

    public int CompareTo(TSXTileset t) => StartTileIndex.CompareTo(t.StartTileIndex);

    public override string ToString() =>
        "Tileset '" + JavaCompat.GetFileName(TilesetImagePath) + " - tileSize=" + TileSize + " - startInd=" + StartTileIndex + " - numTile=" + NumTile;
}

public static class Tiled
{
    public static string GetAdjustedPath(string path, string baseFile) =>
        JavaCompat.IsAbsolutePath(path) ? path : JavaCompat.GetDirectory(baseFile) + path;

    public static List<Tileset> GetTilesets(List<TSXTileset> tsxTilesets, string baseId, Compression compression, bool temp, TileOrdering order)
    {
        var tilesets = new List<Tileset>();
        if (tsxTilesets.Count == 0)
            tilesets.Add(new Tileset(baseId + "_tileset", true));
        else
        {
            int ind = 0;
            foreach (var t in tsxTilesets)
                tilesets.Add(t.GetTileset(baseId + "_tileset" + ind++, compression, temp, order));
        }
        return tilesets;
    }

    public static string GetTSXTilesetPath(string file) => new TSXTileset(file, 0).TilesetPath;

    internal static XmlElement GetElementNamed(List<XmlElement> elements, string name) =>
        elements.FirstOrDefault(e => Xml.Attr(e, "name", "").ToLowerInvariant() == name.ToLowerInvariant());

    internal static void CheckAttributValue(XmlElement e, string attrName, string value, string def, string file)
    {
        string v = Xml.Attr(e, attrName, def).ToLowerInvariant();
        if (v != value)
            throw new Exception("'" + v + "' " + attrName + " not supported (" + def + " expected) in TMX file: " + file);
    }

    internal static void CheckTileSize(int tw, int th, string file)
    {
        if (tw != th)
            throw new Exception("Non square tile not supported (" + tw + " x " + th + ") in TMX file: " + file);
        if ((tw & 7) != 0)
            throw new Exception("Unsuported tile size (should be a multiple of 8) in TMX file: " + file);
        if (tw < 8 || tw > 32)
            throw new Exception(tw + " x " + th + " tile size not supported (only 8x8 to 32x32 allowed) in TMX file: " + file);
    }
}

/// <summary>Tiled のマップ (TMX) の 1 レイヤー。Java 版の TMX.TMXMap。</summary>
public sealed class TMXMap
{
    const int TMX_HFLIP = 1 << 31;
    const int TMX_VFLIP = 1 << 30;
    const int TMX_AXE_FLIP = 1 << 29;
    const int TMX_TILE_IND_MASK = 0x0FFFFFFF;

    public readonly string File;
    public readonly string LayerName;
    public readonly int TileSize;
    public readonly int W;
    public readonly int H;
    public readonly List<TSXTileset> TsxTilesets = new();
    public readonly List<TSXTileset> UsedTilesets;
    public readonly int[] MapData;
    public readonly bool[] PrioMap;

    readonly Dictionary<TSXTileset, byte[]> prioTilesets = new();

    public TMXMap(string file, string layerName)
    {
        if (!System.IO.File.Exists(file))
            throw new FileNotFoundException("TMX file '" + file + " not found !");

        File = file;
        LayerName = layerName;

        var mapElement = Xml.Load(file);
        if (mapElement.Name.ToLowerInvariant() != "map")
            throw new Exception("Expected map root node in TMX file: " + file + ", " + mapElement.Name + " found.");

        Tiled.CheckAttributValue(mapElement, "orientation", "orthogonal", "orthogonal", file);
        Tiled.CheckAttributValue(mapElement, "renderorder", "right-down", "right-down", file);

        int tw = Xml.AttrInt(mapElement, "tilewidth", 0);
        int th = Xml.AttrInt(mapElement, "tileheight", 0);
        Tiled.CheckTileSize(tw, th, file);
        TileSize = tw;

        W = Xml.AttrInt(mapElement, "width", 0);
        H = Xml.AttrInt(mapElement, "height", 0);
        if (W == 0 || H == 0)
            throw new Exception("Null map size in TMX file: " + file);

        var tilesetElements = Xml.Elements(mapElement, "tileset");
        if (tilesetElements.Count == 0)
            throw new Exception("No tileset reference(s) found in TMX file: " + file);

        foreach (var te in tilesetElements)
        {
            int startId = Xml.AttrInt(te, "firstgid", 0);
            string source = Xml.Attr(te, "source", "");
            if (!string.IsNullOrEmpty(source))
                TsxTilesets.Add(new TSXTileset(Tiled.GetAdjustedPath(source, file), startId));
            else
                TsxTilesets.Add(new TSXTileset(file, startId, te));
        }

        foreach (var t in TsxTilesets)
            if (t.TileSize != TileSize)
                throw new Exception("One of the referenced tileset has a different tile size than map tile size in TMX file: " + file);

        // firstgid で並べる (Java の Collections.sort と同じく安定)
        var sorted = TsxTilesets.OrderBy(t => t.StartTileIndex).ToList();
        TsxTilesets.Clear();
        TsxTilesets.AddRange(sorted);

        var layers = Xml.Elements(mapElement, "layer");
        if (layers.Count == 0)
            throw new Exception("No layer found in TMX file: " + file);

        int[] mapData1, mapData2;
        bool singleLayer = Tiled.GetElementNamed(layers, layerName) != null;

        if (singleLayer)
        {
            var mainLayer = Tiled.GetElementNamed(layers, layerName);
            var priorityLayer = Tiled.GetElementNamed(layers, layerName + " priority") ?? Tiled.GetElementNamed(layers, layerName + " prio");
            mapData1 = GetMapData(mainLayer, W, H, file);
            mapData2 = priorityLayer != null ? GetMapData(priorityLayer, W, H, file) : new int[mapData1.Length];
        }
        else
        {
            var low = Tiled.GetElementNamed(layers, layerName + " low");
            var high = Tiled.GetElementNamed(layers, layerName + " high");
            if (low == null || high == null)
                throw new Exception("No layer '" + layerName + "' found in TMX file: " + file);
            mapData1 = GetMapData(low, W, H, file);
            mapData2 = GetMapData(high, W, H, file);
        }

        var usedSet = new HashSet<TSXTileset>();
        var prioIndexesMap = new Dictionary<int, int> { [0] = 0, [-1] = -1 };

        int baseTileSize = TileSize / 8;
        MapData = new int[mapData1.Length];
        PrioMap = new bool[MapData.Length * baseTileSize * baseTileSize];

        int ind = 0;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int v, prio;
                if (singleLayer)
                {
                    v = mapData1[ind];
                    prio = mapData2[ind];
                }
                else if (mapData2[ind] != 0)
                {
                    v = mapData2[ind];
                    prio = -1;
                }
                else
                {
                    v = mapData1[ind];
                    prio = 0;
                }

                bool hflip = (v & TMX_HFLIP) != 0;
                bool vflip = (v & TMX_VFLIP) != 0;
                bool axeflip = (v & TMX_AXE_FLIP) != 0;

                if (axeflip)
                    Console.WriteLine("WARNING: unsupported rotated tile found at [" + x + "," + y + "] in layer '" + layerName + "' of TMX file: " + file);

                int tileInd = v & TMX_TILE_IND_MASK;
                if (tileInd > 0xFFFF)
                    throw new Exception("Tile [" + x + "," + y + "] of layer '" + layerName + "' has an index > 65535 (" + v + "), maximum supported tile index is 65535");

                if (tileInd != 0)
                {
                    var tileset = GetTSXTilesetFor(tileInd)
                        ?? throw new Exception("Tile [" + x + "," + y + "] of layer '" + layerName + "' is referencing an invalid index (" + tileInd + " is outside available tiles range)");
                    usedSet.Add(tileset);
                }

                MapData[ind] = (Tile.TILE_ATTR_FULL(0, false, vflip, hflip, 0) << 16) | tileInd;

                if (!prioIndexesMap.TryGetValue(prio, out int data))
                {
                    data = GetPrio(prio, baseTileSize);
                    prioIndexesMap[prio] = data;
                }

                if (data != 0)
                {
                    int prioInd = (y * W * baseTileSize * baseTileSize) + (x * baseTileSize);
                    int mask = 1 << (baseTileSize * baseTileSize);
                    for (int j = 0; j < baseTileSize; j++)
                    {
                        for (int i = 0; i < baseTileSize; i++)
                        {
                            PrioMap[prioInd] = (data & mask) != 0;
                            mask >>= 1;
                            prioInd++;
                        }
                        prioInd += (W * baseTileSize) - baseTileSize;
                    }
                }

                ind++;
            }
        }

        UsedTilesets = usedSet.OrderBy(t => t.StartTileIndex).ToList();
    }

    int GetPrio(int ind, int baseTileSize)
    {
        if (ind == 0)
            return 0;

        var tsx = GetTSXTilesetFor(ind);
        if (tsx == null)
            return -1;

        if (!prioTilesets.TryGetValue(tsx, out var prioTileset))
        {
            prioTileset = tsx.GetTilesetImage8bpp(true);
            prioTilesets[tsx] = prioTileset;
        }

        int imgW = tsx.ImageTileWidth * TileSize;
        int imgH = tsx.ImageTileHeigt * TileSize;
        var imageTile = new byte[8 * 8];

        int adjTileInd = ind - tsx.StartTileIndex;
        int yt = adjTileInd / tsx.ImageTileWidth;
        int xt = adjTileInd % tsx.ImageTileWidth;
        adjTileInd = (yt * tsx.ImageTileWidth * baseTileSize * baseTileSize) + (xt * baseTileSize);
        int result = 0;

        for (int j = 0; j < baseTileSize; j++)
        {
            for (int i = 0; i < baseTileSize; i++)
            {
                Tile.GetImageTile(prioTileset, imgW, imgH, adjTileInd, 8, imageTile);
                foreach (var d in imageTile)
                {
                    if (d != 0)
                    {
                        result |= 1;
                        break;
                    }
                }
                result <<= 1;
                adjTileInd++;
            }
            adjTileInd += (imgW / 8) - baseTileSize;
        }

        return result;
    }

    TSXTileset GetTSXTilesetFor(int tileInd) => TsxTilesets.FirstOrDefault(t => t.ContainsTile(tileInd));

    public List<Tileset> GetTilesets(string baseId, Compression compression, bool temp, TileOrdering order) =>
        Tiled.GetTilesets(UsedTilesets, baseId, compression, temp, order);

    public byte[] GetMapImage()
    {
        int mapImageW = W * TileSize;
        int mapImageH = H * TileSize;
        var mapImage = new byte[mapImageW * mapImageH];

        var tilesets = new Dictionary<TSXTileset, byte[]>();
        foreach (var t in UsedTilesets)
            tilesets[t] = t.GetTilesetImage8bpp(true);

        var baseTile = new byte[TileSize * TileSize];
        var transformedTile = new byte[TileSize * TileSize];
        int baseTileSize = TileSize / 8;

        int ind = 0;
        for (int yt = 0; yt < H; yt++)
        {
            for (int xt = 0; xt < W; xt++)
            {
                int tile = MapData[ind];
                int tileInd = tile & 0xFFFFFF;
                short tileAttr = (short)((tile >> 16) & 0xFFFF);
                bool hflip = (tileAttr & Tile.TILE_HFLIP_MASK) != 0;
                bool vflip = (tileAttr & Tile.TILE_VFLIP_MASK) != 0;

                byte[] imageTile;
                if (tileInd == 0)
                {
                    Array.Clear(baseTile);
                    imageTile = baseTile;
                }
                else
                {
                    var tsx = GetTSXTilesetFor(tileInd);
                    imageTile = Tile.GetImageTile(tilesets[tsx], tsx.ImageTileWidth * TileSize, tsx.ImageTileHeigt * TileSize,
                        tileInd - tsx.StartTileIndex, TileSize, baseTile);
                }

                if (hflip || vflip)
                    imageTile = Tile.TransformTile(imageTile, TileSize, hflip, vflip, false, transformedTile);

                int prioInd = (yt * W * baseTileSize * baseTileSize) + (xt * baseTileSize);
                for (int j = 0; j < baseTileSize; j++)
                {
                    for (int i = 0; i < baseTileSize; i++)
                    {
                        if (PrioMap[prioInd])
                            Tile.SetPrioTile(imageTile, TileSize, i * 8, j * 8, 8);
                        prioInd++;
                    }
                    prioInd += (W * baseTileSize) - baseTileSize;
                }

                Tile.CopyTile(mapImage, mapImageW, imageTile, xt * TileSize, yt * TileSize, TileSize);
                ind++;
            }
        }

        return mapImage;
    }

    static int[] GetMapData(XmlElement layer, int w, int h, string file)
    {
        if (Xml.AttrInt(layer, "width", w) != w)
            throw new Exception("Layer width do not match map width in TMX file: " + file);
        if (Xml.AttrInt(layer, "height", h) != h)
            throw new Exception("Layer height do not match map height in TMX file: " + file);

        var data = Xml.Element(layer, "data") ?? throw new Exception("Cannot find data XML node in TMX file: " + file);
        Tiled.CheckAttributValue(data, "encoding", "csv", "", file);

        var values = JavaCompat.SplitJava(Xml.FirstValue(data, ""), ",");
        if (values.Length != w * h)
            throw new Exception("Data size (" + values.Length + ") does not match Layer dimension (" + w + " x " + h + ") in TMX file: " + file);

        var result = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
            result[i] = (int)long.Parse(values[i].Trim());
        return result;
    }

    public override string ToString() => "tileSize=" + TileSize + " - w=" + W + " - h=" + H;
}
