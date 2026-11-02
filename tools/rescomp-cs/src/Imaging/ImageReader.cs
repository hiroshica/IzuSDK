using System.Buffers.Binary;
using System.IO.Compression;

namespace Sgdk.Rescomp.Imaging;

/// <summary>
/// 読み込んだ画像。Java の ImageIO で読んだときと同じ値になるようにしてある。
/// </summary>
public sealed class LoadedImage
{
    public int Width;
    public int Height;
    /// <summary>1 画素のビット数 (Java の ColorModel.getPixelSize())。</summary>
    public int Bpp;
    /// <summary>インデックスカラー (グレースケール 1/2/4 ビットを含む) なら true。</summary>
    public bool Indexed;
    /// <summary>8 ビットのグレースケール (Java の TYPE_BYTE_GRAY)。</summary>
    public bool Gray8;
    /// <summary>インデックスカラーのパレット (0xAABBGGRR、Java の IndexColorModel の中身)。</summary>
    public int[] PaletteABGR;
    /// <summary>インデックスカラーかグレースケール 8 ビットの画素 (行ごとにバイト境界へそろえて詰めたもの。Java の DataBufferByte)。</summary>
    public byte[] PackedPixels;
    /// <summary>ARGB (0xAARRGGBB) の画素。</summary>
    public int[] Argb;
}

/// <summary>PNG と BMP を読む (System.Drawing を使わずにどの OS でも動くように自前で読む)。</summary>
public static class ImageReader
{
    static readonly Dictionary<string, LoadedImage> cache = new(StringComparer.Ordinal);

    public static LoadedImage Load(string path)
    {
        var full = Path.GetFullPath(path);
        if (cache.TryGetValue(full, out var img))
            return img;

        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception e)
        {
            throw new IOException("Can't open image '" + path + "'.", e);
        }

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G')
            img = LoadPng(data, path);
        else if (data.Length >= 2 && data[0] == 'B' && data[1] == 'M')
            img = LoadBmp(data, path);
        else
            throw new IOException("Can't open image '" + path + "'.");

        cache[full] = img;
        return img;
    }

    // ---------------- PNG

    static LoadedImage LoadPng(byte[] data, string path)
    {
        int pos = 8;
        int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
        byte[] plte = null, trns = null;
        using var idat = new MemoryStream();

        while (pos + 8 <= data.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            int start = pos + 8;
            if (len < 0 || start + len > data.Length)
                throw new IOException("Image '" + path + "' is corrupted.");
            var chunk = data.AsSpan(start, len);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(chunk);
                    height = BinaryPrimitives.ReadInt32BigEndian(chunk.Slice(4));
                    bitDepth = chunk[8];
                    colorType = chunk[9];
                    interlace = chunk[12];
                    break;
                case "PLTE": plte = chunk.ToArray(); break;
                case "tRNS": trns = chunk.ToArray(); break;
                case "IDAT": idat.Write(chunk); break;
            }
            pos = start + len + 4;
            if (type == "IEND")
                break;
        }

        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new IOException("Image '" + path + "' has an unknown PNG color type.") };
        int bitsPerPixel = channels * bitDepth;
        int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);

        idat.Position = 0;
        byte[] raw;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        using (var ms = new MemoryStream())
        {
            z.CopyTo(ms);
            raw = ms.ToArray();
        }

        // 画素ごとのサンプル値 (チャンネルの並び) に直す
        var samples = new int[width * height * channels];
        int rp = 0;
        if (interlace == 0)
            rp = DecodePass(raw, rp, width, height, channels, bitDepth, bytesPerPixel, samples, width, 0, 0, 1, 1);
        else
        {
            int[] sx = { 0, 4, 0, 2, 0, 1, 0 }, sy = { 0, 0, 4, 0, 2, 0, 1 };
            int[] dx = { 8, 8, 4, 4, 2, 2, 1 }, dy = { 8, 8, 8, 4, 4, 2, 2 };
            for (int p = 0; p < 7; p++)
            {
                int pw = (width - sx[p] + dx[p] - 1) / dx[p];
                int ph = (height - sy[p] + dy[p] - 1) / dy[p];
                if (pw <= 0 || ph <= 0)
                    continue;
                rp = DecodePass(raw, rp, pw, ph, channels, bitDepth, bytesPerPixel, samples, width, sx[p], sy[p], dx[p], dy[p]);
            }
        }

        var img = new LoadedImage { Width = width, Height = height };

        if (colorType == 3 || (colorType == 0 && bitDepth < 8))
        {
            // インデックスカラー (Java は 1/2/4 ビットのグレースケールもインデックスカラーとして読む)
            img.Indexed = true;
            img.Bpp = bitDepth;
            int size = 1 << bitDepth;
            img.PaletteABGR = new int[size];
            if (colorType == 3)
            {
                // Java の PNGImageReader と同じ並べ方: PLTE の数を 2・4・16・256 に切り上げて (足りない分は黒)、
                // それでも 2^bitDepth に足りなければ、切り上げた表の最後の色で埋める
                int n = Math.Min(plte.Length / 3, size);
                int rounded = n > 16 ? 256 : n > 4 ? 16 : n > 2 ? 4 : 2;
                int Entry(int i) => i < n ? (plte[i * 3] | (plte[i * 3 + 1] << 8) | (plte[i * 3 + 2] << 16)) : 0;
                for (int i = 0; i < size; i++)
                {
                    int bgr = i < rounded ? Entry(i) : Entry(rounded - 1);
                    int a = (trns != null && i < trns.Length && i < rounded) ? trns[i] : 0xFF;
                    img.PaletteABGR[i] = (a << 24) | bgr;
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    int v = i * 255 / (size - 1);
                    img.PaletteABGR[i] = unchecked((int)0xFF000000) | (v << 16) | (v << 8) | v;
                }
            }
            img.PackedPixels = PackRows(samples, width, height, bitDepth);
            img.Argb = new int[width * height];
            for (int i = 0; i < img.Argb.Length; i++)
                img.Argb[i] = AbgrToArgb(img.PaletteABGR[samples[i]]);
            return img;
        }

        if (colorType == 0 && bitDepth == 8)
        {
            img.Gray8 = true;
            img.Bpp = 8;
            img.PackedPixels = new byte[width * height];
            img.Argb = new int[width * height];
            for (int i = 0; i < img.PackedPixels.Length; i++)
            {
                int v = samples[i];
                img.PackedPixels[i] = (byte)v;
                img.Argb[i] = unchecked((int)0xFF000000) | (v << 16) | (v << 8) | v;
            }
            return img;
        }

        // トゥルーカラー (16 ビットのグレースケールも含む)
        img.Bpp = bitsPerPixel;
        img.Argb = new int[width * height];
        for (int i = 0; i < img.Argb.Length; i++)
        {
            int o = i * channels;
            int To8(int v) => bitDepth == 16 ? v >> 8 : v;
            int r, g, b, a = 0xFF;
            switch (colorType)
            {
                case 0: r = g = b = To8(samples[o]); break;
                case 4: r = g = b = To8(samples[o]); a = To8(samples[o + 1]); break;
                case 2: r = To8(samples[o]); g = To8(samples[o + 1]); b = To8(samples[o + 2]); break;
                default: r = To8(samples[o]); g = To8(samples[o + 1]); b = To8(samples[o + 2]); a = To8(samples[o + 3]); break;
            }
            img.Argb[i] = (a << 24) | (r << 16) | (g << 8) | b;
        }
        return img;
    }

    static int DecodePass(byte[] raw, int rp, int w, int h, int channels, int bitDepth, int bpp, int[] samples, int fullWidth, int x0, int y0, int dx, int dy)
    {
        int stride = (w * channels * bitDepth + 7) / 8;
        var prev = new byte[stride];
        var cur = new byte[stride];

        for (int y = 0; y < h; y++)
        {
            int filter = raw[rp++];
            Array.Copy(raw, rp, cur, 0, stride);
            rp += stride;
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0;
                int b = prev[i];
                int c = i >= bpp ? prev[i - bpp] : 0;
                int v = cur[i];
                switch (filter)
                {
                    case 1: v += a; break;
                    case 2: v += b; break;
                    case 3: v += (a + b) >> 1; break;
                    case 4:
                        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                        v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
                        break;
                }
                cur[i] = (byte)v;
            }

            int yy = y0 + y * dy;
            for (int x = 0; x < w; x++)
            {
                int xx = x0 + x * dx;
                for (int ch = 0; ch < channels; ch++)
                {
                    int s = x * channels + ch;
                    int v;
                    if (bitDepth == 8) v = cur[s];
                    else if (bitDepth == 16) v = (cur[s * 2] << 8) | cur[s * 2 + 1];
                    else
                    {
                        int bit = s * bitDepth;
                        v = (cur[bit >> 3] >> (8 - bitDepth - (bit & 7))) & ((1 << bitDepth) - 1);
                    }
                    samples[(yy * fullWidth + xx) * channels + ch] = v;
                }
            }
            (prev, cur) = (cur, prev);
        }
        return rp;
    }

    static byte[] PackRows(int[] values, int width, int height, int bitDepth)
    {
        int stride = (width * bitDepth + 7) / 8;
        var result = new byte[stride * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int bit = x * bitDepth;
                result[y * stride + (bit >> 3)] |= (byte)(values[y * width + x] << (8 - bitDepth - (bit & 7)));
            }
        return result;
    }

    // ---------------- BMP

    static LoadedImage LoadBmp(byte[] data, string path)
    {
        int pixOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(10));
        int hdrSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(14));
        int width, height, bitCount, compression = 0, clrUsed = 0;
        if (hdrSize == 12)
        {
            width = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(18));
            height = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(20));
            bitCount = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(24));
        }
        else
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18));
            height = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22));
            bitCount = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(28));
            compression = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(30));
            clrUsed = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(46));
        }
        bool topDown = height < 0;
        height = Math.Abs(height);

        if (compression != 0 && compression != 3)
            throw new NotSupportedException("Image '" + path + "': compressed BMP (RLE) is not supported by the C# rescomp, save it uncompressed or as PNG.");

        var img = new LoadedImage { Width = width, Height = height, Bpp = bitCount };
        int stride = ((width * bitCount + 31) / 32) * 4;

        if (bitCount <= 8)
        {
            img.Indexed = true;
            int n = clrUsed != 0 ? clrUsed : 1 << bitCount;
            int entry = hdrSize == 12 ? 3 : 4;
            int palPos = 14 + hdrSize;
            img.PaletteABGR = new int[n];
            for (int i = 0; i < n; i++)
            {
                int b = data[palPos + i * entry], g = data[palPos + i * entry + 1], r = data[palPos + i * entry + 2];
                img.PaletteABGR[i] = unchecked((int)0xFF000000) | (b << 16) | (g << 8) | r;
            }
            int outStride = (width * bitCount + 7) / 8;
            img.PackedPixels = new byte[outStride * height];
            img.Argb = new int[width * height];
            for (int y = 0; y < height; y++)
            {
                int srcRow = pixOffset + (topDown ? y : height - 1 - y) * stride;
                Array.Copy(data, srcRow, img.PackedPixels, y * outStride, outStride);
                for (int x = 0; x < width; x++)
                {
                    int bit = x * bitCount;
                    int v = (data[srcRow + (bit >> 3)] >> (8 - bitCount - (bit & 7))) & ((1 << bitCount) - 1);
                    img.Argb[y * width + x] = v < n ? AbgrToArgb(img.PaletteABGR[v]) : unchecked((int)0xFF000000);
                }
            }
            return img;
        }

        if (bitCount != 24 && bitCount != 32)
            throw new NotSupportedException("Image '" + path + "': " + bitCount + " bits BMP is not supported by the C# rescomp.");

        img.Argb = new int[width * height];
        int bytes = bitCount / 8;
        for (int y = 0; y < height; y++)
        {
            int srcRow = pixOffset + (topDown ? y : height - 1 - y) * stride;
            for (int x = 0; x < width; x++)
            {
                int o = srcRow + x * bytes;
                int a = bitCount == 32 && compression == 3 ? data[o + 3] : 0xFF;
                img.Argb[y * width + x] = (a << 24) | (data[o + 2] << 16) | (data[o + 1] << 8) | data[o];
            }
        }
        return img;
    }

    public static int AbgrToArgb(int color) =>
        (color & unchecked((int)0xFF00FF00)) | ((color & 0x00FF0000) >> 16) | ((color & 0x000000FF) << 16);
}
