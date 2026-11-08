using System.Text;
using Isdk.Rescomp.Imaging;
using Isdk.Rescomp.Types;

namespace Isdk.Rescomp.Resources;

/// <summary>メタタイルとブロックで圧縮したマップ (Java 版の resource.Map)。</summary>
public sealed class Map : Resource
{
    public static Map GetMap(string id, string imgFile, int mapBase, int metatileSize, List<Tileset> tilesets, Compression compression, bool addTileset)
    {
        var image = ImageUtil.GetImageAs8bpp(imgFile, true, true)
            ?? throw new ArgumentException("RGB image '" + imgFile + "' does not contains palette data (see 'Important note about image format' in the rescomp.txt file");

        foreach (var d in image)
            if ((d & 0x40) != 0)
                throw new ArgumentException("'" + imgFile + "' has color index in [64..127] range, IMAGE resource requires image with a maximum of 64 colors");

        var info = ImageUtil.GetBasicInfo(imgFile);
        int w = info.W;
        int h = image.Length / w;
        return new Map(id, image, w, h, mapBase, metatileSize, tilesets, compression, addTileset);
    }

    public readonly int Wb;
    public readonly int Hb;
    public readonly Compression Compression;
    readonly int hc;

    public readonly List<Metatile> Metatiles = new();
    public readonly List<MapBlock> MapBlocks = new();
    public readonly List<short[]> MapBlockIndexes = new();
    public readonly short[] MapBlockRowOffsets;
    public readonly List<Tileset> Tilesets;

    public readonly Bin MetatilesBin;
    public readonly Bin MapBlocksBin;
    public readonly Bin MapBlockIndexesBin;
    public readonly Bin MapBlockRowOffsetsBin;

    public Map(string id, byte[] image8bpp, int imageWidth, int imageHeight, int mapBase, int metatileSize, List<Tileset> tilesets,
        Compression compression, bool addTileset) : base(id)
    {
        int wt = imageWidth / 8;
        int ht = imageHeight / 8;

        bool mapBasePrio = (mapBase & Tile.TILE_PRIORITY_MASK) != 0;
        int mapBasePal = (mapBase & Tile.TILE_PALETTE_MASK) >> Tile.TILE_PALETTE_SFT;
        int mapBaseTileInd = mapBase & Tile.TILE_INDEX_MASK;
        bool hasBaseTileIndex = mapBaseTileInd != 0;

        Tilesets = tilesets;

        if (addTileset)
        {
            for (int t = 0; t < tilesets.Count; t++)
            {
                var ts = tilesets[t];
                // すでにあるなら内部リソースとして足す (重複している方を使う)
                if (FindResource(ts) != null) tilesets[t] = (Tileset)AddInternalResource(ts);
                // TMX のマップからは参照できないので、グローバルとして足す
                else tilesets[t] = (Tileset)AddResource(ts);
            }
        }

        Compression = compression;

        Wb = (wt + 15) / 16;
        Hb = (ht + 15) / 16;

        var tileset = new Tileset(tilesets);
        MapBlockRowOffsets = new short[Hb];
        Array.Fill(MapBlockRowOffsets, (short)-1);

        for (int j = 0; j < Hb; j++)
        {
            int mbrii = 0;
            var mbRowIndexes = new short[Wb];

            for (int i = 0; i < Wb; i++)
            {
                var mb = new MapBlock();
                int mbi = 0;

                for (int bj = 0; bj < 8; bj++)
                {
                    for (int bi = 0; bi < 8; bi++)
                    {
                        var mt = new Metatile();
                        int mtsi = 0;

                        for (int mj = 0; mj < metatileSize; mj++)
                        {
                            for (int mi = 0; mi < metatileSize; mi++)
                            {
                                int ti = (i * 16) + (bi * 2) + mi;
                                int tj = (j * 16) + (bj * 2) + mj;
                                Tile tile;
                                TileEquality eq;
                                int index;

                                if ((ti >= wt) || (tj >= ht))
                                {
                                    // 画像の外は空のタイル
                                    tile = new Tile(new byte[64], 8, 0, false, 0);
                                    eq = TileEquality.NONE;
                                    index = 0;
                                }
                                else
                                {
                                    tile = Tile.GetTile(image8bpp, wt * 8, ht * 8, ti * 8, tj * 8, 8);

                                    if (hasBaseTileIndex && tile.IsPlain)
                                    {
                                        index = tile.PlainValue;
                                        eq = TileEquality.NONE;
                                    }
                                    else
                                    {
                                        index = tileset.GetTileIndex(tile, TileOptimization.ALL);
                                        if (index == -1)
                                            throw new Exception("Can't find tile [" + ti + "," + tj + "] in tileset, something wrong happened...");
                                        if (index > 2047)
                                            throw new Exception("Can't have more than 2048 different tiles, try to reduce number of unique tile...");
                                        eq = tile.GetEquality(tileset.Get(index));
                                        index += mapBaseTileInd;
                                    }
                                }

                                mt.Set(mtsi++, (short)Tile.TILE_ATTR_FULL(mapBasePal + tile.Pal, mapBasePrio | tile.Prio, eq.VFlip(), eq.HFlip(), index));
                            }
                        }

                        mt.UpdateInternals();

                        int mtIndex = Metatiles.IndexOf(mt);
                        if (mtIndex == -1)
                        {
                            mtIndex = Metatiles.Count;
                            Metatiles.Add(mt);
                        }

                        mb.Set(mbi++, (short)mtIndex);
                    }
                }

                mb.ComputeHashCode();

                int mbIndex = MapBlocks.IndexOf(mb);
                if (mbIndex == -1)
                {
                    mbIndex = MapBlocks.Count;
                    MapBlocks.Add(mb);
                }

                mbRowIndexes[mbrii++] = (short)mbIndex;
            }

            // 同じブロック列がもうあれば、それを指す
            for (int i = 0; i < MapBlockIndexes.Count; i++)
            {
                if (mbRowIndexes.AsSpan().SequenceEqual(MapBlockIndexes[i]))
                {
                    MapBlockRowOffsets[j] = (short)(i * Wb);
                    break;
                }
            }

            if (MapBlockRowOffsets[j] == -1)
            {
                MapBlockRowOffsets[j] = (short)(MapBlockIndexes.Count * Wb);
                MapBlockIndexes.Add(mbRowIndexes);
            }
        }

        var mtData = Metatiles.SelectMany(m => m.Data).ToArray();
        MetatilesBin = (Bin)AddInternalResource(new Bin(id + "_metatiles", mtData, compression));

        if (Metatiles.Count > 256)
        {
            var mbData = MapBlocks.SelectMany(m => m.Data).ToArray();
            MapBlocksBin = (Bin)AddInternalResource(new Bin(id + "_mapBlocks", mbData, compression));
        }
        else
        {
            var mbData = MapBlocks.SelectMany(m => m.Data).Select(s => (byte)s).ToArray();
            MapBlocksBin = (Bin)AddInternalResource(new Bin(id + "_mapBlocks", mbData, compression));
        }

        if (MapBlocks.Count > 256)
        {
            var mbiData = MapBlockIndexes.SelectMany(r => r).ToArray();
            MapBlockIndexesBin = (Bin)AddInternalResource(new Bin(id + "_mapBlockIndexes", mbiData, compression));
        }
        else
        {
            var mbiData = MapBlockIndexes.SelectMany(r => r).Select(s => (byte)s).ToArray();
            MapBlockIndexesBin = (Bin)AddInternalResource(new Bin(id + "_mapBlockIndexes", mbiData, compression));
        }

        // 行オフセットは小さいので圧縮しない
        MapBlockRowOffsetsBin = (Bin)AddInternalResource(new Bin(id + "_mapBlockRowOffsets", MapBlockRowOffsets, Compression.NONE));

        if (compression != Compression.NONE)
        {
            int ts = TotalSize();
            if (!hasBaseTileIndex)
            {
                ts -= MetatilesBin.TotalSize();
                ts += Metatiles.Count * 4 * 2;
            }

            if (ts > 48 * 1024)
                throw new Exception("Error: MAP '" + id + "' unpacked size = " + ts
                    + " bytes, you won't have enough memory to unpack it.\nRemove compression from MAP resource definition");
            if (ts > 34 * 1024)
                Console.Error.WriteLine("Warning: MAP '" + id + "' unpacked size = " + ts
                    + " bytes, you may not be able to unpack it.\nYou may remove compression from MAP resource definition");
        }

        hc = tileset.GetHashCode() ^ MetatilesBin.GetHashCode() ^ MapBlocksBin.GetHashCode() ^ MapBlockIndexesBin.GetHashCode()
            ^ MapBlockRowOffsetsBin.GetHashCode();
    }

    public int Width => Wb;
    public int Height => Hb;

    int GetCompression()
    {
        int result = (int)MapBlockIndexesBin.PackedData.Compression - 1;
        result <<= 4;
        result += (int)MapBlocksBin.PackedData.Compression - 1;
        result <<= 4;
        result += (int)MetatilesBin.PackedData.Compression - 1;
        return result;
    }

    public override int InternalHashCode() => hc;

    public override bool InternalEquals(object obj) =>
        obj is Map m && Metatiles.SequenceEqual(m.Metatiles) && MapBlocks.SequenceEqual(m.MapBlocks)
            && MapBlockIndexesBin.Equals(m.MapBlockIndexesBin) && MapBlockRowOffsets.AsSpan().SequenceEqual(m.MapBlockRowOffsets);

    public override List<Bin> GetInternalBinResources() => new() { MetatilesBin, MapBlocksBin, MapBlockIndexesBin, MapBlockRowOffsetsBin };

    public override int ShallowSize() => 2 + 2 + 2 + 2 + 4 + 4 + 4 + 4 + 4 + 4;

    public override int TotalSize() =>
        MetatilesBin.TotalSize() + MapBlocksBin.TotalSize() + MapBlockIndexesBin.TotalSize() + MapBlockRowOffsetsBin.TotalSize() + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "MapDefinition", Id, 2, Global);
        outS.Append("    dc.w    " + Wb + ", " + Hb + "\n");
        outS.Append("    dc.w    " + MapBlockIndexes.Count + "\n");
        outS.Append("    dc.w    " + GetCompression() + "\n");
        outS.Append("    dc.w    " + Metatiles.Count + "\n");
        outS.Append("    dc.w    " + MapBlocks.Count + "\n");
        outS.Append("    dc.l    " + MetatilesBin.Id + "\n");
        outS.Append("    dc.l    " + MapBlocksBin.Id + "\n");
        outS.Append("    dc.l    " + MapBlockIndexesBin.Id + "\n");
        outS.Append("    dc.l    " + MapBlockRowOffsetsBin.Id + "\n");
        outS.Append("\n");
    }

    public override string ToString() =>
        "MAP '" + Id + "' details: " + Tilesets.Count + " tilesets, " + Metatiles.Count + " metatiles, " + MapBlocks.Count
            + " blocks, block grid size = " + Wb + " x " + Hb + " - optimized = " + Wb + " x " + MapBlockIndexes.Count;

    /// <summary>Java の Compiler と同じく、種類ごとにまとめて並べる (圧縮が効きやすい)。</summary>
    public static IEnumerable<Bin> CollectBins(List<Resource> maps, bool far)
    {
        var list = maps.Cast<Map>().ToList();
        foreach (var m in list) if (m.MetatilesBin.Far == far) yield return m.MetatilesBin;
        foreach (var m in list) if (m.MapBlockIndexesBin.Far == far) yield return m.MapBlockIndexesBin;
        foreach (var m in list) if (m.MapBlockRowOffsetsBin.Far == far) yield return m.MapBlockRowOffsetsBin;
        foreach (var m in list) if (m.MapBlocksBin.Far == far) yield return m.MapBlocksBin;
    }
}
