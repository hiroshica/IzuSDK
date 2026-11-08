namespace Isdk.Rescomp.Types;

/// <summary>8x8 のタイル (Java 版の type.Tile)。data は 1 要素に 4bpp の画素 8 つ。</summary>
public sealed class Tile : IComparable<Tile>
{
    public const int TILE_MAX_NUM = 1 << 11;
    public const int TILE_INDEX_MASK = TILE_MAX_NUM - 1;

    public const int TILE_HFLIP_SFT = 11;
    public const int TILE_VFLIP_SFT = 12;
    public const int TILE_PALETTE_SFT = 13;
    public const int TILE_PRIORITY_SFT = 15;

    public const int TILE_HFLIP_FLAG = 1 << TILE_HFLIP_SFT;
    public const int TILE_VFLIP_FLAG = 1 << TILE_VFLIP_SFT;
    public const int TILE_PRIORITY_FLAG = 1 << TILE_PRIORITY_SFT;

    public const int TILE_HFLIP_MASK = TILE_HFLIP_FLAG;
    public const int TILE_VFLIP_MASK = TILE_VFLIP_FLAG;
    public const int TILE_PALETTE_MASK = 3 << TILE_PALETTE_SFT;
    public const int TILE_PRIORITY_MASK = TILE_PRIORITY_FLAG;

    public const int TILE_ATTR_MASK = TILE_PRIORITY_MASK | TILE_PALETTE_MASK | TILE_VFLIP_MASK | TILE_HFLIP_MASK;

    public static int TILE_ATTR(int pal, int prio, int flipV, int flipH) =>
        (flipH << TILE_HFLIP_SFT) + (flipV << TILE_VFLIP_SFT) + (pal << TILE_PALETTE_SFT) + (prio << TILE_PRIORITY_SFT);

    public static int TILE_ATTR_FULL(int pal, int prio, int flipV, int flipH, int index) =>
        TILE_ATTR(pal, prio, flipV, flipH) + index;

    public static int TILE_ATTR(int pal, bool prio, bool flipV, bool flipH) =>
        TILE_ATTR(pal, prio ? 1 : 0, flipV ? 1 : 0, flipH ? 1 : 0);

    public static int TILE_ATTR_FULL(int pal, bool prio, bool flipV, bool flipH, int index) =>
        TILE_ATTR_FULL(pal, prio ? 1 : 0, flipV ? 1 : 0, flipH ? 1 : 0, index);

    public static byte[] TransformTile(byte[] tile8bpp, int size, bool hflip, bool vflip, bool prio, byte[] dest)
    {
        int dstOffset = 0;
        int baseOffset = vflip ? (size - 1) * size : 0;
        for (int j = 0; j < size; j++)
        {
            int offset = baseOffset + (hflip ? size - 1 : 0);
            for (int i = 0; i < size; i++)
            {
                dest[dstOffset++] = (byte)(tile8bpp[offset] | (prio ? 0x80 : 0));
                if (hflip) offset--; else offset++;
            }
            if (vflip) baseOffset -= size; else baseOffset += size;
        }
        return dest;
    }

    public static byte[] FlipTile(byte[] tile8bpp, int size, bool hflip, bool vflip, bool prio) =>
        TransformTile(tile8bpp, size, hflip, vflip, prio, new byte[size * size]);

    public static void SetPrioTile(byte[] dest, int imgW, int x, int y, int size)
    {
        int dstOff = (y * imgW) + x;
        for (int j = 0; j < size; j++)
        {
            for (int i = 0; i < size; i++)
                dest[dstOff++] |= 0x80;
            dstOff += imgW - size;
        }
    }

    public static void CopyTile(byte[] dest, int imgW, byte[] tile8bpp, int x, int y, int size)
    {
        int dstOff = (y * imgW) + x;
        int srcOff = 0;
        for (int j = 0; j < size; j++)
        {
            for (int i = 0; i < size; i++)
                dest[dstOff++] = tile8bpp[srcOff++];
            dstOff += imgW - size;
        }
    }

    public static byte[] GetImageTile(byte[] image8bpp, int imgW, int imgH, int x, int y, int size, byte[] dest)
    {
        int srcOff = (y * imgW) + x;
        int dstOff = 0;
        for (int j = y; j < y + size; j++)
        {
            for (int i = x; i < x + size; i++)
            {
                dest[dstOff++] = (i >= 0 && i < imgW && j >= 0 && j < imgH) ? image8bpp[srcOff] : (byte)0;
                srcOff++;
            }
            srcOff += imgW - size;
        }
        return dest;
    }

    public static byte[] GetImageTile(byte[] image8bpp, int imgW, int imgH, int x, int y, int size) =>
        GetImageTile(image8bpp, imgW, imgH, x, y, size, new byte[size * size]);

    public static byte[] GetImageTile(byte[] image8bpp, int imgW, int imgH, int tileInd, int size, byte[] dest)
    {
        int wt = imgW / size;
        return GetImageTile(image8bpp, imgW, imgH, (tileInd % wt) * size, (tileInd / wt) * size, size, dest);
    }

    public static byte[] GetImageTile(byte[] image8bpp, int imgW, int imgH, int tileInd, int size)
    {
        int wt = imgW / size;
        return GetImageTile(image8bpp, imgW, imgH, (tileInd % wt) * size, (tileInd / wt) * size, size, new byte[size * size]);
    }

    /// <summary>8bpp の画像からタイルを取り出す (b0-b3 = 色、b4-b5 = パレット、b7 = 優先度)。</summary>
    public static Tile GetTile(byte[] image8bpp, int imgW, int imgH, int x, int y, int size)
    {
        var imageTile = GetImageTile(image8bpp, imgW, imgH, x, y, size);
        var data = new byte[size * size];

        int plainCol = -1;
        bool plain = true;
        int pal = -1, prio = -1, transPal = -1, transPrio = -1;

        int off = 0;
        for (int j = 0; j < size; j++)
        {
            for (int i = 0; i < size; i++)
            {
                // Java の byte は符号つきなので、パレットと優先度はそのまま取り出す
                int pixel = (sbyte)imageTile[off];
                int color = pixel & 0xF;

                if (plainCol == -1)
                    plainCol = color;
                else if (plainCol != color)
                    plain = false;

                int curPal = (pixel >> 4) & 3;
                int curPrio = (pixel >> 7) & 1;

                if (color == 0)
                {
                    if (transPal == -1)
                        transPal = curPal;
                    else if (transPal != curPal)
                        throw new ArgumentException("Error: transparent pixel at [" + (x + i) + "," + (y + j) + "] reference a different palette (" + curPal + " != " + transPal + ").");

                    if (transPrio == -1)
                        transPrio = curPrio;
                    else if (transPrio != curPrio)
                        throw new ArgumentException("Error: transparent pixel at [" + (x + i) + "," + (y + j) + "] reference a different priority (" + curPrio + " != " + transPrio + ").");
                }
                else
                {
                    if (pal == -1)
                        pal = curPal;
                    else if (pal != curPal)
                        throw new ArgumentException("Error: pixel at [" + (x + i) + "," + (y + j) + "] reference a different palette (" + curPal + " != " + pal + ").");

                    if (prio == -1)
                        prio = curPrio;
                    else if (prio != curPrio)
                        throw new ArgumentException("Error: pixel at [" + (x + i) + "," + (y + j) + "] reference a different priority (" + curPrio + " != " + prio + ").");
                }

                data[off] = (byte)color;
                off++;
            }
        }

        if (pal == -1) pal = transPal;
        if (prio == -1) prio = transPrio;

        return new Tile(data, size, pal, prio != 0, plain ? plainCol : -1);
    }

    public readonly int[] Data;
    public readonly int Size;
    public readonly int Pal;
    public readonly int Plain;
    public readonly bool Prio;
    public readonly bool Empty;

    readonly int[] hFlip;
    readonly int[] vFlip;
    readonly int[] hvFlip;
    readonly int hc;

    public Tile(int[] data, int size, int pal, bool prio, int plain)
    {
        if (data.Length != (size * size) / 8)
            throw new ArgumentException("new Tile(int[]) error: array lenght does not match tile size !");

        Data = data;
        Size = size;
        Pal = pal & 3;
        Prio = prio;
        Plain = plain;
        Empty = data.All(d => d == 0);

        hFlip = GetFlipped(true, false);
        vFlip = GetFlipped(false, true);
        hvFlip = GetFlipped(true, true);

        hc = unchecked(GetHash(data) + GetHash(hFlip) + GetHash(vFlip) + GetHash(hvFlip));
    }

    public Tile(byte[] pixel8bpp, int size, int pal, bool prio, int plain)
        : this(JavaCompat.ByteToInt(Imaging.ImageUtil.ConvertTo4bpp(pixel8bpp, 8)), size, pal, prio, plain) { }

    static int GetHash(int[] array)
    {
        int result = 0;
        foreach (var v in array)
            result = unchecked(result + v);
        return result;
    }

    public bool IsBlank => Empty;
    public bool IsPlain => Plain != -1;
    public int PlainValue => Plain;

    static int SwapNibble32(int value)
    {
        uint v = (uint)value;
        uint r = 0;
        for (int n = 0; n < 8; n++)
            r |= ((v >> (n * 4)) & 0xF) << ((7 - n) * 4);
        return (int)r;
    }

    int[] GetFlipped(bool hflip, bool vflip)
    {
        var result = new int[Data.Length];
        int rowSize = Size / 8;
        int dstOffset = 0;
        int baseOffset = vflip ? (Size - 1) * rowSize : 0;

        for (int j = 0; j < Size; j++)
        {
            int offset = baseOffset + (hflip ? rowSize - 1 : 0);
            for (int i = 0; i < rowSize; i++)
            {
                if (hflip)
                {
                    result[dstOffset] = SwapNibble32(Data[offset]);
                    offset--;
                }
                else
                {
                    result[dstOffset] = Data[offset];
                    offset++;
                }
                dstOffset++;
            }
            if (vflip) baseOffset -= rowSize; else baseOffset += rowSize;
        }
        return result;
    }

    public TileEquality GetEquality(Tile tile)
    {
        if (tile.Data.AsSpan().SequenceEqual(Data)) return TileEquality.EQUAL;
        return GetFlipEquality(tile);
    }

    public TileEquality GetFlipEquality(Tile tile)
    {
        if (tile.Data.AsSpan().SequenceEqual(hFlip)) return TileEquality.HFLIP;
        if (tile.Data.AsSpan().SequenceEqual(vFlip)) return TileEquality.VFLIP;
        if (tile.Data.AsSpan().SequenceEqual(hvFlip)) return TileEquality.HVFLIP;
        return TileEquality.NONE;
    }

    public override int GetHashCode() => hc;

    public override bool Equals(object obj) =>
        obj is Tile t ? hc == t.hc && Data.AsSpan().SequenceEqual(t.Data) : ReferenceEquals(this, obj);

    public int CompareTo(Tile tile) => hc.CompareTo(tile.hc);

    public override string ToString() => "[" + string.Join(", ", Data) + "]";
}
