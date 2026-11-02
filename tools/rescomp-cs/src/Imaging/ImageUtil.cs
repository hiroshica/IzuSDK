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
}
