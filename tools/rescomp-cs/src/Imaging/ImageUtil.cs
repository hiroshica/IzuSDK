using System.Text.RegularExpressions;

namespace Sgdk.Rescomp.Imaging;

/// <summary>Java 版の sgdk.tool.ImageUtil のうち、rescomp が使うもの。</summary>
public static class ImageUtil
{
    public readonly record struct BasicImageInfo(int W, int H, int Bpp);

    public static BasicImageInfo GetBasicInfo(string filename)
    {
        var img = ImageReader.Load(filename);
        return new BasicImageInfo(img.Width, img.Height, img.Bpp);
    }

    public static int[] GetARGBPixels(string filename) => ImageReader.Load(filename).Argb;

    /// <summary>インデックスカラーの画素 (行ごとにバイト境界へ詰めたもの)。</summary>
    public static byte[] GetIndexedPixels(string filename)
    {
        var img = ImageReader.Load(filename);
        if (!img.Gray8 && !img.Indexed)
            throw new ArgumentException("Image '" + filename + "' is RGB, only indexed images (8bpp, 4bpp, 2bpp and 1bpp) are supported !");
        return img.PackedPixels;
    }

    static int? GetTilePlainColor(int[] argb, int imgWidth, int xt, int yt)
    {
        int off = (yt * 8 * imgWidth) + (xt * 8);
        int col = argb[off];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
                if (argb[off++] != col)
                    return null;
            off += imgWidth - 8;
        }
        return col;
    }

    /// <summary>RGB 画像の左上 4 行の 8x8 タイルからパレットを読む (0xAABBGGRR、65 番目は背景色)。</summary>
    public static int[] GetRGBA8888PaletteFromTiles(int[] argb, int w, int h)
    {
        if (w < 128)
            throw new Exception("Error: RGB image width should be >= 128 to store palette data !");
        if (h < 32)
            throw new Exception("Error: RGB image height should be >= 32 to store palette data !");

        var palette = new int[4 * 16 + 1];
        int off = 0;
        for (int p = 0; p < 4; p++)
            for (int c = 0; c < 16; c++)
            {
                var plain = GetTilePlainColor(argb, w, c, p);
                if (plain == null)
                    throw new Exception("Error: expected a plain tile for palette data at position [" + p + "," + c + "] (see rescomp.txt for more info about RGB image) !");
                palette[off++] = ArgbToAbgr(plain.Value);
            }

        palette[64] = palette[0];
        if (w >= 136)
        {
            var backdrop = GetTilePlainColor(argb, w, 16, 0);
            if (backdrop != null)
                palette[64] = ArgbToAbgr(backdrop.Value);
        }
        return palette;
    }

    public static int[] GetRGBA8888PaletteFromTiles(string filename)
    {
        var info = GetBasicInfo(filename);
        var argb = GetARGBPixels(filename);
        int[] result;
        try
        {
            result = GetRGBA8888PaletteFromTiles(argb, info.W, info.H);
        }
        catch (Exception e)
        {
            throw new Exception(e.Message + "\nFile: '" + filename + "'");
        }
        result[0] = result[64];
        return result[..64];
    }

    public static short[] GetRGBA4444PaletteFromTiles(string filename, int mask) =>
        ConvertRGBA8888toRGBA4444(GetRGBA8888PaletteFromTiles(filename), mask);

    /// <summary>Paint Shop Pro の .pal ファイル。</summary>
    public static int[] GetRGBA8888PaletteFromPALFile(string file)
    {
        using var br = new StreamReader(file);
        var line = br.ReadLine();
        if (line != "JASC-PAL")
            throw new ArgumentException("'" + file + "' is not a valid Paint Shop Pro .pal file !");
        br.ReadLine();
        int size = int.Parse(br.ReadLine());
        var result = new int[size];
        for (int i = 0; i < size; i++)
        {
            var text = Regex.Replace(br.ReadLine(), "#[0-9a-fA-F]+", "");
            var rgba = Regex.Split(text, "\\s+").ToList();
            // Java の split は後ろの空の要素を捨てる
            while (rgba.Count > 1 && rgba[^1].Length == 0)
                rgba.RemoveAt(rgba.Count - 1);

            int r, g, b, a;
            if (rgba.Count < 3)
            {
                r = g = b = int.Parse(rgba[0]);
                a = 0xFF;
            }
            else
            {
                r = int.Parse(rgba[0]);
                g = int.Parse(rgba[1]);
                b = int.Parse(rgba[2]);
                a = rgba.Count > 3 ? JavaCompat.ParseInt(rgba[3], 0xFF) : 0xFF;
            }
            result[i] = (a << 24) | (b << 16) | (g << 8) | r;
        }
        return result;
    }

    public static short[] GetRGBA4444PaletteFromPALFile(string file, int mask) =>
        ConvertRGBA8888toRGBA4444(GetRGBA8888PaletteFromPALFile(file), mask);

    /// <summary>インデックスカラー画像のパレット (0xAABBGGRR)。</summary>
    public static int[] GetRGBA8888PaletteFromIndColImage(string filename)
    {
        var img = ImageReader.Load(filename);
        if (img.Gray8)
        {
            var result = new int[256];
            for (int i = 0; i < result.Length; i++)
                result[i] = unchecked((int)0xFF000000) | (i << 16) | (i << 8) | i;
            return result;
        }
        if (!img.Indexed)
            throw new ArgumentException("Image '" + filename + "' is RGB, only indexed images (8bpp, 4bpp, 2bpp and 1bpp) are supported !");
        return (int[])img.PaletteABGR.Clone();
    }

    public static short[] GetRGBA4444PaletteFromIndColImage(string filename, int mask) =>
        ConvertRGBA8888toRGBA4444(GetRGBA8888PaletteFromIndColImage(filename), mask);

    public static int ArgbToAbgr(int color) =>
        (color & unchecked((int)0xFF00FF00)) | ((color & 0x00FF0000) >> 16) | ((color & 0x000000FF) << 16);

    public static short[] ConvertRGBA8888toRGBA4444(int[] pixels, int mask = 0xFFFF)
    {
        if (pixels == null)
            return null;
        var result = new short[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            int p = pixels[i];
            int a = (p & unchecked((int)0xF0000000)) >> 28;
            int b = (p & 0x00F00000) >> 20;
            int g = (p & 0x0000F000) >> 12;
            int r = (p & 0x000000F0) >> 4;
            result[i] = (short)(((a << 12) | (b << 8) | (g << 4) | r) & mask);
        }
        return result;
    }

    public static int AbgrToArgb(int color) => ArgbToAbgr(color);

    /// <summary>
    /// 画像を 8bpp の画素にする (b0-b3 = 色、b4-b5 = パレット、b7 = 優先度)。
    /// RGB 画像は左上のパレットのタイルから色を探す。パレットが見つからなければ null。
    /// </summary>
    public static byte[] GetImageAs8bpp(string imgFile, bool checkTileAligned, bool removeRGBPalette)
    {
        var info = GetBasicInfo(imgFile);
        if (checkTileAligned)
        {
            if ((info.W & 7) != 0)
                throw new ArgumentException("'" + imgFile + "' width is '" + info.W + ", should be a multiple of 8.");
            if ((info.H & 7) != 0)
                throw new ArgumentException("'" + imgFile + "' height is '" + info.H + ", should be a multiple of 8.");
        }

        if (info.Bpp > 8)
            return ConvertRGBTo8bpp(imgFile, removeRGBPalette);

        return ConvertTo8bpp(GetIndexedPixels(imgFile), info.Bpp);
    }

    public static byte[] ConvertTo4bpp(byte[] data, int inputBpp)
    {
        switch (inputBpp)
        {
            case 1:
            {
                var r = new byte[data.Length * 4];
                for (int i = 0; i < data.Length; i++)
                    for (int k = 0; k < 4; k++)
                        r[i * 4 + k] = (byte)((((data[i] >> (7 - 2 * k)) & 1) << 4) | ((data[i] >> (6 - 2 * k)) & 1));
                return r;
            }
            case 2:
            {
                var r = new byte[data.Length * 2];
                for (int i = 0; i < data.Length; i++)
                {
                    r[i * 2] = (byte)((((data[i] >> 6) & 3) << 4) | ((data[i] >> 4) & 3));
                    r[i * 2 + 1] = (byte)((((data[i] >> 2) & 3) << 4) | (data[i] & 3));
                }
                return r;
            }
            case 4:
                return data;
            case 8:
            {
                var r = new byte[data.Length / 2];
                for (int i = 0; i < r.Length; i++)
                    r[i] = (byte)(((data[i * 2] & 0xF) << 4) | (data[i * 2 + 1] & 0xF));
                return r;
            }
            default:
                throw new ArgumentException("Not supported image data format (" + inputBpp + " bpp)");
        }
    }

    public static byte[] ConvertTo8bpp(byte[] data, int inputBpp)
    {
        switch (inputBpp)
        {
            case 1:
            case 2:
            case 4:
            {
                int ppb = 8 / inputBpp;
                int mask = (1 << inputBpp) - 1;
                var r = new byte[data.Length * ppb];
                for (int i = 0; i < data.Length; i++)
                    for (int k = 0; k < ppb; k++)
                        r[i * ppb + k] = (byte)((data[i] >> (8 - inputBpp * (k + 1))) & mask);
                return r;
            }
            case 8:
                return data;
            default:
                throw new ArgumentException("Not supported image data format (" + inputBpp + " bpp)");
        }
    }

    public static byte[] GetSubImage(byte[] image8bpp, int imageW, int rx, int ry, int rw, int rh)
    {
        var result = new byte[rw * rh];
        int src = (ry * imageW) + rx;
        int dst = 0;
        for (int y = 0; y < rh; y++)
        {
            Array.Copy(image8bpp, src, result, dst, rw);
            src += imageW;
            dst += rw;
        }
        return result;
    }

    /// <summary>RGB 画像を、左上のパレットのタイル (4 行 x 16 色) を使って 8bpp にする。</summary>
    public static byte[] ConvertRGBTo8bpp(string filename, bool cropPalette)
    {
        var info = GetBasicInfo(filename);
        int w = info.W;
        int h = info.H;

        if ((w & 7) != 0)
            throw new ArgumentException("'" + filename + "' width is '" + w + ", should be a multiple of 8 for RGB image.");
        if ((h & 7) != 0)
            throw new ArgumentException("'" + filename + "' height is '" + h + ", should be a multiple of 8 for RGB image.");

        var argb = GetARGBPixels(filename);

        int[] palette;
        try
        {
            palette = GetRGBA8888PaletteFromTiles(argb, w, h);
        }
        catch (Exception e)
        {
            throw new Exception(e.Message + "\nFile: '" + filename + "'");
        }

        // 色から番号 (小さい順) を引く表
        var colorMap = new Dictionary<int, List<int>>();
        for (int c = 0; c < 64; c++)
        {
            int color = AbgrToArgb(palette[c]);
            if (!colorMap.TryGetValue(color, out var list))
                colorMap[color] = list = new List<int>();
            list.Add(c);
        }

        int wt = w / 8;
        int ht = h / 8;
        var result = new byte[w * h];

        for (int yt = 0; yt < 4; yt++)
            for (int xt = 0; xt < wt; xt++)
            {
                int off = (yt * 8 * w) + (xt * 8);
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                        result[off++] = (byte)(xt < 16 ? (yt * 16) + xt : 0);
                    off += w - 8;
                }
            }

        for (int yt = 0; yt < ht; yt++)
        {
            for (int xt = 0; xt < wt; xt++)
            {
                int off = (yt * 8 * w) + (xt * 8);

                if (yt < 4 && xt < 16)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                            result[off++] = (byte)((yt * 16) + xt);
                        off += w - 8;
                    }
                }
                else if (yt == 0 && xt == 16)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                            result[off++] = 0;
                        off += w - 8;
                    }
                }
                else
                {
                    // Java の HashSet<Integer> では 0/16/32/48 は同じ入れ物に入れた順に並ぶので、順番つきのリストで同じになる
                    if (!colorMap.TryGetValue(argb[off], out var indexes) || indexes.Count == 0)
                        throw new Exception("'" + filename + "': pixel at position [" + (xt * 8) + "," + (yt * 8) + "] uses a color not present in palette.");

                    var pals = new List<int>();
                    foreach (var index in indexes)
                        if (!pals.Contains(index & 0xF0))
                            pals.Add(index & 0xF0);

                    for (int y = 0; y < 8; y++)
                    {
                        if (pals.Count <= 1)
                            break;
                        for (int x = 0; x < 8; x++)
                        {
                            if (pals.Count <= 1)
                                break;

                            if (!colorMap.TryGetValue(argb[off], out indexes) || indexes.Count == 0)
                                throw new Exception("'" + filename + "': pixel at position [" + ((xt * 8) + x) + "," + ((yt * 8) + y) + "] uses a color not present in palette.");

                            var curPals = indexes.Select(i => i & 0xF0).ToHashSet();
                            pals.RemoveAll(p => !curPals.Contains(p));

                            if (pals.Count == 0)
                                throw new Exception("'" + filename + "': pixels at position [" + ((xt * 8) + x) + "," + ((yt * 8) + y) + "] use color from a different palette.");

                            off++;
                        }
                        off += w - 8;
                    }

                    int pal = pals[0];
                    off = (yt * 8 * w) + (xt * 8);

                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            bool done = false;
                            if (!colorMap.TryGetValue(argb[off], out indexes) || indexes.Count == 0)
                                throw new Exception("'" + filename + "': pixel at position [" + ((xt * 8) + x) + "," + ((yt * 8) + y) + "] uses a color not present in palette.");

                            foreach (var index in indexes)
                            {
                                if ((index & 0xF0) == pal)
                                {
                                    result[off] = (byte)index;
                                    done = true;
                                    break;
                                }
                            }

                            if (!done)
                                throw new Exception("'" + filename + "': pixel at position [" + ((xt * 8) + x) + "," + ((yt * 8) + y) + "] use color from a different palette.");

                            off++;
                        }
                        off += w - 8;
                    }
                }
            }
        }

        if (cropPalette)
            result = GetSubImage(result, w, 0, 4 * 8, w, h - (4 * 8));

        return result;
    }

    /// <summary>8bpp のインデックスカラー PNG を書く (パレットは 0xAARRGGBB)。</summary>
    public static void SaveIndexedPng(string path, int w, int h, int[] paletteARGB, byte[] pixels)
    {
        var plte = new byte[paletteARGB.Length * 3];
        var trns = new byte[paletteARGB.Length];
        bool hasAlpha = false;
        for (int i = 0; i < paletteARGB.Length; i++)
        {
            plte[i * 3] = (byte)(paletteARGB[i] >> 16);
            plte[i * 3 + 1] = (byte)(paletteARGB[i] >> 8);
            plte[i * 3 + 2] = (byte)paletteARGB[i];
            trns[i] = (byte)(paletteARGB[i] >> 24);
            hasAlpha |= trns[i] != 0xFF;
        }
        var raw = new byte[h * (w + 1)];
        for (int y = 0; y < h; y++)
            Array.Copy(pixels, y * w, raw, y * (w + 1) + 1, w);
        WritePng(path, new byte[] { (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w, (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h, 8, 3, 0, 0, 0 },
            plte, hasAlpha ? trns : null, raw);
    }

    static void WritePng(string path, byte[] ihdr, byte[] plte, byte[] trns, byte[] raw)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        void Chunk(string type, byte[] data)
        {
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            fs.Write(new byte[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length });
            fs.Write(typeBytes);
            fs.Write(data);
            uint crc = Crc32(data, Crc32(typeBytes, 0xFFFFFFFF, false), true);
            fs.Write(new byte[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
        }

        Chunk("IHDR", ihdr);
        if (plte != null)
            Chunk("PLTE", plte);
        if (trns != null)
            Chunk("tRNS", trns);
        using (var ms = new MemoryStream())
        {
            using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                z.Write(raw);
            Chunk("IDAT", ms.ToArray());
        }
        Chunk("IEND", Array.Empty<byte>());
    }

    static uint[] crcTable;

    // typeBytes と data を続けて計算するため、途中の値を受け渡す
    static uint Crc32(byte[] data, uint crc, bool final)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                crcTable[n] = c;
            }
        }
        foreach (var b in data)
            crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return final ? crc ^ 0xFFFFFFFF : crc;
    }

    // ---- スプライトの分割で使うもの (Java 版と同じく、矩形は画像の範囲で切ってから調べる)

    public static bool IsTransparent(byte[] image8bpp, int imgW, int imgH, Types.JRect region)
    {
        var adj = region.Intersection(new Types.JRect(imgW, imgH));
        int offset = (adj.Y * imgW) + adj.X;
        for (int y = 0; y < adj.Height; y++)
        {
            for (int x = 0; x < adj.Width; x++)
                if (image8bpp[offset++] != 0)
                    return false;
            offset += imgW - adj.Width;
        }
        return true;
    }

    public static int GetOpaquePixelCount(byte[] image8bpp, int imgW, int imgH, Types.JRect region)
    {
        var adj = region.Intersection(new Types.JRect(imgW, imgH));
        int result = 0;
        int offset = (adj.Y * imgW) + adj.X;
        for (int y = 0; y < adj.Height; y++)
        {
            for (int x = 0; x < adj.Width; x++)
                if (image8bpp[offset++] != 0)
                    result++;
            offset += imgW - adj.Width;
        }
        return result;
    }

    public static bool HasOpaquePixelOnEdge(byte[] image8bpp, int imgW, int imgH, Types.JRect region, bool left, bool top, bool right, bool bottom)
    {
        var adj = region.Intersection(new Types.JRect(imgW, imgH));
        if (left)
        {
            int offset = (adj.Y * imgW) + adj.X;
            for (int i = adj.Y; i < adj.Y + adj.Height; i++)
            {
                if (image8bpp[offset] != 0) return true;
                offset += imgW;
            }
        }
        if (top)
        {
            int offset = (adj.Y * imgW) + adj.X;
            for (int i = adj.X; i < adj.X + adj.Width; i++)
            {
                if (image8bpp[offset] != 0) return true;
                offset++;
            }
        }
        if (right)
        {
            int offset = (adj.Y * imgW) + adj.X + (adj.Width - 1);
            for (int i = adj.Y; i < adj.Y + adj.Height; i++)
            {
                if (image8bpp[offset] != 0) return true;
                offset += imgW;
            }
        }
        if (bottom)
        {
            int offset = ((adj.Y + (adj.Height - 1)) * imgW) + adj.X;
            for (int i = adj.X; i < adj.X + adj.Width; i++)
            {
                if (image8bpp[offset] != 0) return true;
                offset++;
            }
        }
        return false;
    }

    public static Types.JRect GetOpaqueRect(byte[] image8bpp, int imgW, int imgH, Types.JRect region)
    {
        var adj = region.Intersection(new Types.JRect(imgW, imgH));
        var result = new Types.JRect(0, 0, -1, -1);
        var pt = new Types.JRect(0, 0, 1, 1);
        for (int j = adj.Y; j < adj.Y + adj.Height; j++)
        {
            int offset = j * imgW;
            for (int i = adj.X; i < adj.X + adj.Width; i++)
            {
                if (image8bpp[offset + i] != 0)
                {
                    pt.X = i; pt.Y = j;
                    result.Add(pt);
                }
            }
        }
        return result;
    }

    /// <summary>スプライトが使うパレットの番号 (1 つだけ使える)。</summary>
    public static int GetSpritePaletteIndex(byte[] image8bpp, int imgW, int imgH)
    {
        int pal = -1;
        int srcOff = 0;
        for (int y = 0; y < imgH; y++)
            for (int x = 0; x < imgW; x++)
            {
                int pixel = image8bpp[srcOff++];
                if ((pixel & 0xF) != 0)
                {
                    int curPal = (pixel >> 4) & 0xF;
                    if (pal == -1)
                        pal = curPal;
                    else if (pal != curPal)
                        throw new ArgumentException("Error: pixel at [" + x + "," + y + "] reference a different palette (" + curPal + " != " + pal + ").");
                }
            }
        return pal == -1 ? 0 : pal;
    }

    /// <summary>
    /// 画像を 2 倍に拡大し、矩形 (x, y, w, h は拡大後の座標) をピンクで描いて PNG で保存する (スプライトの分割の確認用)。
    /// </summary>
    public static void SaveScaledWithRects(string srcFile, string path, IEnumerable<(int X, int Y, int W, int H)> rects)
    {
        var img = ImageReader.Load(srcFile);
        int w = img.Width * 2, h = img.Height * 2;
        var px = new int[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = img.Argb[(y / 2) * img.Width + (x / 2)];

        const int pink = unchecked((int)0xFFFFAFAF);
        void Plot(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < w && y < h)
                px[y * w + x] = pink;
        }
        foreach (var r in rects)
        {
            // Graphics2D.drawRect と同じく、右端と下端は x + w、y + h の位置に描く
            for (int x = r.X; x <= r.X + r.W; x++) { Plot(x, r.Y); Plot(x, r.Y + r.H); }
            for (int y = r.Y; y <= r.Y + r.H; y++) { Plot(r.X, y); Plot(r.X + r.W, y); }
        }
        SaveArgbPng(path, w, h, px);
    }

    /// <summary>32 ビット (ARGB) の画素を RGBA の PNG で保存する。</summary>
    public static void SaveArgbPng(string path, int w, int h, int[] argb)
    {
        var raw = new byte[h * (1 + w * 4)];
        int o = 0;
        for (int y = 0; y < h; y++)
        {
            raw[o++] = 0;
            for (int x = 0; x < w; x++)
            {
                int c = argb[y * w + x];
                raw[o++] = (byte)(c >> 16);
                raw[o++] = (byte)(c >> 8);
                raw[o++] = (byte)c;
                raw[o++] = (byte)(c >> 24);
            }
        }
        WritePng(path, new byte[] { (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w, (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h, 8, 6, 0, 0, 0 }, null, null, raw);
    }
}
