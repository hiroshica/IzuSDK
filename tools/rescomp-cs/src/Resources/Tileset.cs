using System.Text;
using Isdk.Rescomp.Imaging;
using Isdk.Rescomp.Types;

namespace Isdk.Rescomp.Resources;

/// <summary>タイルの集まり (Java 版の resource.Tileset)。</summary>
public sealed class Tileset : Resource
{
    public static Tileset GetTileset(string id, string imgFile, Compression compression, TileOptimization tileOpt, bool addBlank, bool temp,
        TileOrdering order, bool export)
    {
        var image = ImageUtil.GetImageAs8bpp(imgFile, true, true)
            ?? throw new ArgumentException("RGB image '" + imgFile + "' does not contains palette data (see 'Important note about image format' in the rescomp.txt file");

        var info = ImageUtil.GetBasicInfo(imgFile);
        int w = info.W;
        // RGB 画像はパレットの部分を縦に切り取るので、高さはデータの長さから求める
        int h = image.Length / w;

        var result = new Tileset(id, image, w, h, 0, 0, w / 8, h / 8, tileOpt, compression, addBlank, temp, order);

        if (export)
        {
            var tilesetImage = result.GetTilesetImage();
            // ABGR で読んだパレットを ARGB に戻す
            var palette = info.Bpp > 8 ? ImageUtil.GetRGBA8888PaletteFromTiles(imgFile) : ImageUtil.GetRGBA8888PaletteFromIndColImage(imgFile);
            var argb = new int[16];
            for (int i = 0; i < 16; i++)
                argb[i] = i < palette.Length ? ImageUtil.AbgrToArgb(palette[i]) : 0;
            // Java 版は 0 番を透明にする
            argb[0] &= 0x00FFFFFF;
            ImageUtil.SaveIndexedPng(JavaCompat.SetExtension(imgFile, "-tileset-export.png"), 16 * 8, tilesetImage.Length / (16 * 8), argb, tilesetImage);
        }

        return result;
    }

    readonly List<Tile> tiles = new();
    readonly Dictionary<Tile, int> tileIndexesMap = new();
    readonly Dictionary<int, List<Tile>> tileByHashcodeMap = new();
    readonly int hc;

    public readonly Bin Bin;
    public readonly bool IsDuplicate;

    /// <summary>TSX のように、いくつかのタイルセットを最適化せずにつなげる (一時的なもの)。</summary>
    public Tileset(List<Tileset> tilesets) : base("tilesets")
    {
        foreach (var tileset in tilesets)
            foreach (var tile in tileset.tiles)
                Add(tile);

        Bin = new Bin(Id + "_data", BuildData(), Compression.NONE);
        hc = Bin.GetHashCode();
    }

    /// <summary>空のタイルセット。</summary>
    public Tileset() : base("empty_tileset")
    {
        Bin = new Bin("empty_bin", Array.Empty<byte>(), Compression.NONE);
        hc = Bin.GetHashCode();
    }

    /// <summary>TSX 用 (空白のタイル 1 つだけ、または空)。</summary>
    public Tileset(string id, bool blankTile) : base(id)
    {
        if (blankTile)
            Add(new Tile(new int[8], 8, 0, false, 0));
        Bin = new Bin(id + "_data", BuildData(), Compression.NONE);
        hc = Bin.GetHashCode();
    }

    public Tileset(string id, byte[] image8bpp, int imageWidth, int imageHeight, int startTileX, int startTileY, int widthTile, int heightTile,
        TileOptimization opt, Compression compression, bool addBlank, bool temp, TileOrdering order) : base(id)
    {
        bool hasBlank = false;

        // Tileset と Tilemap / Map を作るときは、必ず同じ順番で回すこと
        void Process(int i, int j)
        {
            var tile = Tile.GetTile(image8bpp, imageWidth, imageHeight, (i + startTileX) * 8, (j + startTileY) * 8, 8);
            int index = GetTileIndex(tile, opt);
            hasBlank |= tile.IsBlank;
            if (index == -1)
                Add(tile);
        }

        if (order == TileOrdering.ROW)
        {
            for (int j = 0; j < heightTile; j++)
                for (int i = 0; i < widthTile; i++)
                    Process(i, j);
        }
        else
        {
            for (int i = 0; i < widthTile; i++)
                for (int j = 0; j < heightTile; j++)
                    Process(i, j);
        }

        if (!hasBlank && addBlank)
            Add(new Tile(new int[8], 8, 0, false, 0));

        (Bin, IsDuplicate) = StoreBin(BuildData(), compression, temp);
        hc = Bin.GetHashCode();
    }

    /// <summary>スプライト用: 矩形ごとに、縦に並べてタイルを取り出す (最適化しない)。</summary>
    public Tileset(string id, byte[] image8bpp, int imageWidth, int imageHeight, IEnumerable<(int X, int Y, int W, int H)> sprites, Compression compression, bool temp)
        : base(id)
    {
        foreach (var rect in sprites)
        {
            int widthTile = rect.W / 8;
            int heightTile = rect.H / 8;
            for (int i = 0; i < widthTile; i++)
                for (int j = 0; j < heightTile; j++)
                    Add(Tile.GetTile(image8bpp, imageWidth, imageHeight, rect.X + (i * 8), rect.Y + (j * 8), 8));
        }

        (Bin, IsDuplicate) = StoreBin(BuildData(), compression, temp);
        hc = Bin.GetHashCode();
    }

    (Bin, bool) StoreBin(int[] data, Compression compression, bool temp)
    {
        var binResource = new Bin(Id + "_data", data, compression) { Global = false };
        // 一時的なタイルセットなら BIN は登録しない
        if (temp)
            return (binResource, false);
        bool duplicate = FindResource(binResource) != null;
        return ((Bin)AddInternalResource(binResource), duplicate);
    }

    int[] BuildData()
    {
        var data = new int[tiles.Count * 8];
        int offset = 0;
        foreach (var t in tiles)
        {
            Array.Copy(t.Data, 0, data, offset, 8);
            offset += 8;
        }
        return data;
    }

    public int NumTile => tiles.Count;
    public bool IsEmpty => NumTile == 0;
    public Tile Get(int index) => tiles[index];
    public IReadOnlyList<Tile> Tiles => tiles;

    public void Add(Tile tile)
    {
        // Java の HashMap.put と同じく、同じタイルがあれば番号を新しいものにする
        tileIndexesMap[tile] = tiles.Count;
        if (!tileByHashcodeMap.TryGetValue(tile.GetHashCode(), out var list))
            tileByHashcodeMap[tile.GetHashCode()] = list = new List<Tile>();
        list.Add(tile);
        tiles.Add(tile);
    }

    public int GetTileIndex(Tile tile, TileOptimization opt)
    {
        if (opt == TileOptimization.NONE)
            return -1;

        if (tileIndexesMap.TryGetValue(tile, out var key))
            return key;

        if (opt == TileOptimization.ALL && tileByHashcodeMap.TryGetValue(tile.GetHashCode(), out var hashTiles))
        {
            foreach (var t in hashTiles)
                if (t.GetFlipEquality(tile) != TileEquality.NONE)
                    return tileIndexesMap[t];
        }

        return -1;
    }

    /// <summary>横 16 タイルに並べた 8bpp の画像。</summary>
    public byte[] GetTilesetImage()
    {
        int w = 16;
        int h = (tiles.Count + 15) / w;
        int imgW = w * 8;
        int imgH = h * 8;
        var image = new byte[imgW * imgH];

        int ind = 0;
        for (int y = 0; y < imgH; y += 8)
            for (int x = 0; x < imgW; x += 8)
            {
                var imageTile = ImageUtil.ConvertTo8bpp(JavaCompat.IntToByte(tiles[ind].Data), 4);
                Tile.CopyTile(image, imgW, imageTile, x, y, 8);
                if (++ind >= tiles.Count)
                    return image;
            }
        return image;
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) => obj is Tileset t && Bin.Equals(t.Bin);
    public override List<Bin> GetInternalBinResources() => new() { Bin };
    public override int ShallowSize() => 2 + 2 + 4;
    public override int TotalSize() => Bin.TotalSize() + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "TileSet", Id, 2, Global);
        // BIN を先に出力してあるので、実際に使った圧縮がわかる
        outS.Append("    dc.w    " + ((int)Bin.DoneCompression - 1) + "\n");
        outS.Append("    dc.w    " + NumTile + "\n");
        outS.Append("    dc.l    " + Bin.Id + "\n");
        outS.Append("\n");
    }
}
