using System.Text;
using Sgdk.Rescomp.Packers;

namespace Sgdk.Rescomp;

/// <summary>Java 版の sgdk.rescomp.tool.Util にあたるもの。</summary>
public static class Util
{
    static readonly string[] formatAsm = { "b", "b", "w", "w", "d" };

    public static short ToVDPColor(byte b, byte g, byte r)
    {
        int ri = ((sbyte)r >> 4) & 0xE;
        int gi = ((sbyte)g >> 4) & 0xE;
        int bi = ((sbyte)b >> 4) & 0xE;
        return (short)((ri << 0) | (gi << 4) | (bi << 8));
    }

    public static SoundDriver GetSoundDriver(string text)
    {
        var up = text.ToUpperInvariant();
        if (up.Length == 0 || up == "PCM" || up == "DEFAULT") return SoundDriver.PCM;
        if (up == "DPCM2") return SoundDriver.DPCM2;
        if (up == "PCM4") return SoundDriver.PCM4;
        if (up == "XGM") return SoundDriver.XGM;
        if (up == "XGM2") return SoundDriver.XGM2;
        if (up == "0") throw new ArgumentException("'" + text + "' is not anymore recognized as valid sound driver, use 'PCM' instead.");
        if (up == "1") throw new ArgumentException("'" + text + "' is not anymore recognized as valid sound driver, use 'DPCM2' instead.");
        if (up == "2") throw new ArgumentException("'" + text + "' is not anymore recognized as valid sound driver, use 'PCM4' instead.");
        if (up == "3" || up == "4" || up == "5") throw new ArgumentException("'" + text + "' is not anymore recognized as valid sound driver, use 'XGM' instead.");
        throw new ArgumentException("Unrecognized sound driver: '" + text + "'");
    }

    public static Compression GetCompression(string text)
    {
        var up = text.ToUpperInvariant();
        if (up == "AUTO" || up == "BEST" || up == "-1") return Compression.AUTO;
        if (up.Length == 0 || up == "NONE" || up == "0") return Compression.NONE;
        if (up == "APLIB" || up == "1") return Compression.APLIB;
        if (up == "LZ4W" || up == "2" || up == "FAST") return Compression.LZ4W;
        throw new ArgumentException("Unrecognized compression: '" + text + "'");
    }

    public static CollisionType GetCollision(string text)
    {
        var up = text.ToUpperInvariant();
        if (up.Length == 0 || up == "NONE") return CollisionType.NONE;
        if (up == "BOX") return CollisionType.BOX;
        if (up == "CIRCLE") return CollisionType.CIRCLE;
        throw new ArgumentException("Unrecognized collision: '" + text + "'");
    }

    public static TileOptimization GetTileOpt(string text)
    {
        var up = text.ToUpperInvariant();
        if (up == "NONE" || up == "0") return TileOptimization.NONE;
        if (up.Length == 0 || up == "ALL" || up == "1") return TileOptimization.ALL;
        if (up == "DUPLICATE" || up == "2") return TileOptimization.DUPLICATE_ONLY;
        throw new ArgumentException("Unrecognized tilemap optimization: '" + text + "'");
    }

    public static Types.OptimizationType GetSpriteOptType(string text)
    {
        var up = text.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(up) || up == "BALANCED" || up == "0") return Types.OptimizationType.BALANCED;
        if (up == "SPRITE" || up == "1") return Types.OptimizationType.MIN_SPRITE;
        if (up == "TILE" || up == "2") return Types.OptimizationType.MIN_TILE;
        if (up == "NONE" || up == "3") return Types.OptimizationType.NONE;
        throw new ArgumentException("Unrecognized sprite optimization type: '" + text + "'");
    }

    public static Types.OptimizationLevel GetSpriteOptLevel(string text)
    {
        var up = text.ToUpperInvariant();
        int value = JavaCompat.ParseInt(up, -1);
        if (string.IsNullOrWhiteSpace(up) || up == "FAST" || value == 0) return Types.OptimizationLevel.FAST;
        if (up == "MEDIUM" || value == 1) return Types.OptimizationLevel.MEDIUM;
        // 以前の、くり返しの回数で指定する書き方
        if (up == "MAX" || value > 200000) return Types.OptimizationLevel.MAX;
        if (up == "SLOW" || value >= 10000) return Types.OptimizationLevel.SLOW;
        if (value != -1) return Types.OptimizationLevel.FAST;
        throw new ArgumentException("Unrecognized sprite optimization level: '" + text + "'");
    }

    public static TileOrdering GetTileOrdering(string text)
    {
        var up = text.ToUpperInvariant();
        if (up == "COLUMN") return TileOrdering.COLUMN;
        if (up == "ROW") return TileOrdering.ROW;
        throw new ArgumentException("Unrecognized tile ordering: '" + text + "'");
    }

    public static byte[] SizeAlign(byte[] data, int align, byte fill)
    {
        int newSize = (data.Length + (align - 1)) / align * align;
        var result = new byte[newSize];
        Array.Copy(data, result, data.Length);
        for (int i = data.Length; i < result.Length; i++)
            result[i] = fill;
        return result;
    }

    public static void Decl(StringBuilder outS, StringBuilder outH, string type, string name, int align, bool global)
    {
        outS.Append("    .align " + (align < 2 ? 2 : align) + "\n");
        if (global)
            outS.Append("    .global " + name + "\n");
        outS.Append(name + ":\n");
        if (global)
            outH.Append("extern const " + type + " " + name + ";\n");
    }

    public static void DeclArray(StringBuilder outS, StringBuilder outH, string type, string name, int size, int align, bool global)
    {
        outS.Append("    .align  " + (align < 2 ? 2 : align) + "\n");
        if (global)
            outS.Append("    .global " + name + "\n");
        outS.Append(name + ":\n");
        if (global)
            outH.Append("extern const " + type + " " + name + "[" + size + "];\n");
    }

    public static void DeclArrayEnd(StringBuilder outS, StringBuilder outH, string type, string name, int size, int align, bool global)
    {
        if (global)
            outS.Append("    .global " + name + "_size\n");
        outS.Append(name + "_size = .-" + name + "\n");
    }

    /// <summary>データを dc.b / dc.w / dc.l の行で書く (1 行 16 バイト)。</summary>
    public static void OutS(StringBuilder outS, byte[] data, int intSize)
    {
        int offset = 0;
        int remain = data.Length;

        while (remain > 0)
        {
            outS.Append("    dc." + formatAsm[intSize] + "    ");
            for (int i = 0; i < Math.Min(16, remain) / intSize; i++)
            {
                if (i > 0)
                    outS.Append(", ");
                outS.Append("0x");
                offset += intSize;
                for (int j = 0; j < intSize; j++)
                    outS.Append(JavaCompat.ToHexaString(data[offset - (j + 1)], 2));
            }
            outS.Append("\n");
            remain -= 16;
        }

        // ワードにそろえておく
        if (intSize == 1 && (data.Length & 1) != 0)
            outS.Append("    dc.b    0x00\n");
    }

    /// <summary>outB の位置をそろえる。2 より大きいそろえ方は詰める量がわからないので outB を空にして false。</summary>
    public static bool Align(BinaryStream outB, int align)
    {
        if (align > 2)
        {
            outB.Reset();
            return false;
        }
        if (align == 2 && (outB.Size & 1) != 0)
            outB.Write(0);
        return true;
    }

    public static void OutB(BinaryStream outB, byte[] data, int align = 0)
    {
        if (Align(outB, align))
            outB.Write(data);
    }

    public static bool IsCompressionValuable(Compression method, int compressedSize, int uncompressedSize)
    {
        int minDiff = method == Compression.APLIB ? 120 : 60;
        if ((uncompressedSize - compressedSize) <= minDiff)
            return false;
        double maxPercentage = method == Compression.APLIB ? 85 : 95;
        return JavaCompat.Round((compressedSize * 100f) / uncompressedSize) <= maxPercentage;
    }

    public static PackedData Pack(byte[] data, Compression compression, BinaryStream bin, bool forceSelectedCompression = false)
    {
        if (compression == Compression.NONE)
            return new PackedData(data, Compression.NONE);

        if (forceSelectedCompression && compression == Compression.AUTO)
            throw new ArgumentException("Cannot use AUTO compression !");

        if (compression == Compression.LZ4W)
        {
            var packed = Lz4wPack(bin?.ToArray(), data);

            if (forceSelectedCompression)
            {
                if (packed == null)
                    throw new InvalidOperationException("Cannot use wanted compression on resource, try removing compression.");
            }
            else if (packed == null || !IsCompressionValuable(Compression.LZ4W, packed.Length, data.Length))
                return new PackedData(data, Compression.NONE);

            return new PackedData(packed, Compression.LZ4W);
        }

        // AUTO は数えない
        int n = Enum.GetValues<Compression>().Length - 1;
        var results = new byte[n][];
        var sizes = new int[n];
        results[0] = data;
        sizes[0] = data.Length;

        byte[] prevData = (bin != null && bin.Size > 1) ? bin.ToArray() : null;

        foreach (var comp in Enum.GetValues<Compression>())
        {
            if (comp == Compression.AUTO || comp == Compression.NONE)
                continue;

            if (compression == Compression.AUTO || compression == comp)
            {
                int compIndex = (int)comp - 1;
                byte[] packed = comp switch
                {
                    Compression.APLIB => ApPack(data),
                    Compression.LZ4W => Lz4wPack(prevData, data),
                    _ => null,
                };

                if (forceSelectedCompression)
                {
                    if (packed == null)
                        throw new InvalidOperationException("Cannot use desired compression on resource ! Try removing compression.");
                    return new PackedData(packed, comp);
                }

                if (packed != null)
                {
                    results[compIndex] = packed;
                    sizes[compIndex] = packed.Length;
                }
            }
        }

        int minSize = sizes[0];
        byte[] result = results[0];
        var bestComp = Compression.NONE;

        foreach (var comp in Enum.GetValues<Compression>())
        {
            if (comp == Compression.AUTO || comp == Compression.NONE)
                continue;
            int compIndex = (int)comp - 1;
            if (results[compIndex] != null && IsCompressionValuable(comp, sizes[compIndex], sizes[0]) && sizes[compIndex] < minSize)
            {
                minSize = sizes[compIndex];
                result = results[compIndex];
                bestComp = comp;
            }
        }

        return new PackedData(result, bestComp);
    }

    public static byte[] ApPack(byte[] data)
    {
        try
        {
            return Apj.Pack(data, false);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return null;
        }
    }

    public static byte[] Lz4wPack(byte[] prev, byte[] data)
    {
        int prevLen = prev?.Length ?? 0;
        var buf = new byte[prevLen + data.Length];
        if (prev != null)
            Array.Copy(prev, buf, prevLen);
        Array.Copy(data, 0, buf, prevLen, data.Length);

        try
        {
            try
            {
                return Lz4w.Pack(buf, prevLen);
            }
            catch (ArgumentException)
            {
                // 前のデータを使わずに圧縮し直す
                return Lz4w.Pack(data, 0);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return null;
        }
    }

    // DPCM の差分表 (Java 版の Util.delta_tab)
    static readonly int[] deltaTab = { -34, -21, -13, -8, -5, -3, -2, -1, 0, 1, 2, 3, 5, 8, 13, 21 };

    static int GetBestDeltaIndex(int wantedLevel, int curLevel)
    {
        int wdelta = wantedLevel - curLevel;
        int ind = 0;
        int mindiff = Math.Abs(wdelta - deltaTab[ind]);
        for (int i = 1; i < 16; i++)
        {
            int diff = Math.Abs(wdelta - deltaTab[i]);
            if (diff < mindiff)
            {
                mindiff = diff;
                ind = i;
            }
        }
        int newLevel = deltaTab[ind] + curLevel;
        // 8 ビット符号付きからはみ出すなら一つ戻す
        if (newLevel > 127) return ind - 1;
        if (newLevel < -128) return ind + 1;
        return ind;
    }

    /// <summary>8 ビット符号付き PCM を 4 ビット DPCM にする (Java 版の Util.dpcmPack)。</summary>
    public static byte[] DpcmPack(byte[] input)
    {
        var result = new byte[(input.Length / 2) + (input.Length & 1)];
        int curLevel = 0;
        int offDst = 0;
        for (int off = 0; off < input.Length; off += 2)
        {
            int ind = GetBestDeltaIndex((sbyte)input[off], curLevel);
            curLevel += deltaTab[ind];
            int o = ind;
            ind = GetBestDeltaIndex(off + 1 < input.Length ? (sbyte)input[off + 1] : 0, curLevel);
            curLevel += deltaTab[ind];
            o |= ind << 4;
            result[offDst++] = (byte)o;
        }
        return result;
    }
}
