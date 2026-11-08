using System.Text;
using Isdk.Rescomp.Imaging;
using Isdk.Rescomp.Types;

namespace Isdk.Rescomp.Resources;

/// <summary>タイルマップ (Java 版の resource.Tilemap)。</summary>
public sealed class Tilemap : Resource
{
    public static Tilemap GetTilemap(string id, Tileset tileset, int mapBase, byte[] image8bpp, int imageWidth, int imageHeight, int startTileX, int startTileY,
        int widthTile, int heightTile, TileOptimization opt, Compression compression, TileOrdering order)
    {
        int w = widthTile;
        int h = heightTile;

        bool mapBasePrio = (mapBase & Tile.TILE_PRIORITY_MASK) != 0;
        int mapBasePal = (mapBase & Tile.TILE_PALETTE_MASK) >> Tile.TILE_PALETTE_SFT;
        int mapBaseTileInd = mapBase & Tile.TILE_INDEX_MASK;
        // タイルの番号をずらすときは、システムの単色タイルを使える
        bool useSystemTiles = mapBaseTileInd != 0;

        var data = new short[w * h];
        int offset = 0;
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                int ti = i + startTileX;
                int tj = j + startTileY;

                var tile = Tile.GetTile(image8bpp, imageWidth, imageHeight, ti * 8, tj * 8, 8);
                int index;
                var equality = TileEquality.NONE;

                if (opt == TileOptimization.NONE)
                {
                    index = order == TileOrdering.ROW ? (j * w) + i + mapBaseTileInd : (i * h) + j + mapBaseTileInd;
                }
                else if (useSystemTiles && tile.IsPlain)
                {
                    index = tile.PlainValue;
                }
                else
                {
                    index = tileset.GetTileIndex(tile, opt);
                    if (index == -1)
                        throw new Exception("Can't find tile [" + ti + "," + tj + "] in tileset, something wrong happened...");
                    equality = tile.GetEquality(tileset.Get(index));
                    index += mapBaseTileInd;
                }

                data[offset++] = (short)Tile.TILE_ATTR_FULL(mapBasePal + tile.Pal, mapBasePrio | tile.Prio, equality.VFlip(), equality.HFlip(), index);
            }
        }

        return new Tilemap(id, data, w, h, compression);
    }

    public static Tilemap GetTilemap(string id, Tileset tileset, int mapBase, byte[] image8bpp, int widthTile, int heightTile, TileOptimization opt,
        Compression compression, TileOrdering order) =>
        GetTilemap(id, tileset, mapBase, image8bpp, widthTile * 8, heightTile * 8, 0, 0, widthTile, heightTile, opt, compression, order);

    public static Tilemap GetTilemap(string id, Tileset tileset, int mapBase, string imgFile, TileOptimization tileOpt, Compression compression, TileOrdering order)
    {
        var image = ImageUtil.GetImageAs8bpp(imgFile, true, true)
            ?? throw new ArgumentException("RGB image '" + imgFile + "' does not contains palette data (see 'Important note about image format' in the rescomp.txt file");
        var info = ImageUtil.GetBasicInfo(imgFile);
        int w = info.W;
        int h = image.Length / w;
        return GetTilemap(id, tileset, mapBase, image, w / 8, h / 8, tileOpt, compression, order);
    }

    public readonly int W;
    public readonly int H;
    public readonly Bin Bin;
    readonly int hc;

    public Tilemap(string id, short[] data, int w, int h, Compression compression) : base(id)
    {
        W = w;
        H = h;
        Bin = (Bin)AddInternalResource(new Bin(id + "_data", data, compression));
        hc = Bin.GetHashCode() ^ (w << 8) ^ (h << 16);
    }

    public short[] GetData() => JavaCompat.ByteToShort(Bin.Data);

    public void SetData(short[] data)
    {
        if (data.Length != W * H)
            throw new Exception("Tilemap.setData(..): size do not match !");
        var bytes = JavaCompat.ShortToByte(data);
        Array.Copy(bytes, Bin.Data, Bin.Data.Length);
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) => obj is Tilemap t && W == t.W && H == t.H && Bin.Equals(t.Bin);
    public override List<Bin> GetInternalBinResources() => new() { Bin };
    public override int ShallowSize() => 2 + 2 + 2 + 4;
    public override int TotalSize() => Bin.TotalSize() + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "TileMap", Id, 2, Global);
        outS.Append("    dc.w    " + ((int)Bin.DoneCompression - 1) + "\n");
        outS.Append("    dc.w    " + W + ", " + H + "\n");
        outS.Append("    dc.l    " + Bin.Id + "\n");
        outS.Append("\n");
    }
}
