using System.Text;

namespace Sgdk.Rescomp;

/// <summary>
/// rescomp.jar と同じ出力にするための、Java の振る舞いの写し (SGDK の tools/commons の StringUtil・FileUtil など)。
/// </summary>
public static class JavaCompat
{
    /// <summary>Java の Math.round(float)。</summary>
    public static int Round(float value) => (int)MathF.Floor(value + 0.5f);

    /// <summary>Java の Math.round(double)。</summary>
    public static long Round(double value) => (long)Math.Floor(value + 0.5d);

    /// <summary>StringUtil.toHexaString(value, size): 小文字の 16 進数で、桁数をそろえる。</summary>
    public static string ToHexaString(int value, int size)
    {
        var result = ((uint)value).ToString("x");
        if (result.Length > size)
            return result.Substring(result.Length - size);
        return result.PadLeft(size, '0');
    }

    /// <summary>StringUtil.parseInt: Integer.parseInt と同じ書式 (10 進、符号付き) で読めなければ def。</summary>
    public static int ParseInt(string s, int def)
    {
        if (s == null || s.Length == 0)
            return def;
        int i = 0;
        bool neg = false;
        if (s[0] == '-' || s[0] == '+')
        {
            neg = s[0] == '-';
            i = 1;
            if (s.Length == 1)
                return def;
        }
        long v = 0;
        for (; i < s.Length; i++)
        {
            int d = s[i] - '0';
            if (d < 0 || d > 9)
                return def;
            v = v * 10 + d;
            if (v > (neg ? 2147483648L : int.MaxValue))
                return def;
        }
        return (int)(neg ? -v : v);
    }

    /// <summary>StringUtil.parseBoolean: "true" / "false" (大文字小文字を区別しない) 以外は def。</summary>
    public static bool ParseBoolean(string s, bool def)
    {
        if (s == null)
            return def;
        var v = s.ToLowerInvariant();
        if (v == "true") return true;
        if (v == "false") return false;
        return def;
    }

    /// <summary>Java の String.trim() (U+0020 以下の文字を両端から取る)。</summary>
    public static string Trim(string s)
    {
        int start = 0, end = s.Length;
        while (start < end && s[start] <= ' ') start++;
        while (end > start && s[end - 1] <= ' ') end--;
        return s.Substring(start, end - start);
    }

    /// <summary>Java の String.hashCode()。</summary>
    public static int HashCode(string s)
    {
        int h = 0;
        foreach (char c in s)
            h = unchecked(31 * h + c);
        return h;
    }

    /// <summary>Java の Arrays.hashCode(byte[])。</summary>
    public static int HashCode(byte[] a)
    {
        if (a == null) return 0;
        int h = 1;
        foreach (byte b in a)
            h = unchecked(31 * h + (sbyte)b);
        return h;
    }

    /// <summary>
    /// java.util.HashSet&lt;String&gt; を回したときの順番に並べる (中身は重複のない、加えた順の列)。
    /// 表の大きさは 16 から始まり、要素数が 0.75 倍を超えると倍になる。同じ場所に入るものは加えた順のまま。
    /// </summary>
    public static List<string> JavaHashSetOrder(IReadOnlyList<string> items)
    {
        int cap = 16;
        while (items.Count > cap * 3 / 4)
            cap *= 2;
        return items
            .Select((s, i) => (s, i, b: Spread(HashCode(s)) & (cap - 1)))
            .OrderBy(t => t.b).ThenBy(t => t.i)
            .Select(t => t.s)
            .ToList();

        static int Spread(int h) => h ^ (int)((uint)h >> 16);
    }

    // ---- FileUtil

    /// <summary>FileUtil.getGenericPath: \ を / にする。</summary>
    public static string GenericPath(string path) => path?.Replace('\\', '/');

    /// <summary>FileUtil.getDirectory(path, sep)。</summary>
    public static string GetDirectory(string path, bool sep = true)
    {
        var p = GenericPath(path);
        if (!string.IsNullOrEmpty(p))
        {
            int index = p.LastIndexOf('/');
            if (index != -1)
                return p.Substring(0, index + (sep ? 1 : 0));
            index = p.LastIndexOf(':');
            if (index != -1)
                return p.Substring(0, index + 1);
        }
        return "";
    }

    /// <summary>FileUtil.getFileName(path, withExtension)。</summary>
    public static string GetFileName(string path, bool withExtension = true)
    {
        var p = GenericPath(path);
        if (string.IsNullOrEmpty(p))
            return "";
        int index = p.LastIndexOf('/');
        string fileName;
        if (index != -1)
            fileName = p.Substring(index + 1);
        else
        {
            index = p.LastIndexOf(':');
            fileName = index != -1 ? p.Substring(index + 1) : p;
        }
        if (withExtension)
            return fileName;
        index = fileName.LastIndexOf('.');
        if (index == 0) return "";
        return index != -1 ? fileName.Substring(0, index) : fileName;
    }

    /// <summary>FileUtil.getFileExtension(path, withDot)。</summary>
    public static string GetFileExtension(string path, bool withDot)
    {
        var p = GenericPath(path);
        if (string.IsNullOrEmpty(p))
            return "";
        int index = p.LastIndexOf('.');
        if (index == -1 || p.IndexOf('/', index) != -1)
            return "";
        return withDot ? p.Substring(index) : p.Substring(index + 1);
    }

    /// <summary>FileUtil.setExtension(path, extension)。</summary>
    public static string SetExtension(string path, string extension)
    {
        var p = GenericPath(path);
        if (string.IsNullOrEmpty(p))
            return "";
        int len = p.Length;
        var result = p;
        int dot = result.LastIndexOf('.');
        if (dot >= 0 && (len - dot) <= 5)
        {
            if ((dot + 1) == len || !char.IsDigit(result[dot + 1]))
                result = result.Substring(0, dot);
        }
        if (extension != null)
            result += extension;
        return result;
    }

    /// <summary>Java の File.isAbsolute()。</summary>
    public static bool IsAbsolutePath(string path) => Path.IsPathFullyQualified(path);

    /// <summary>
    /// FileUtil.adjustPath(dir, path): 相対パスなら new File(dir, path).getAbsolutePath()。
    /// Java と同じく ".." は畳まず、区切り文字は OS のものにする。
    /// </summary>
    public static string AdjustPath(string dir, string path)
    {
        if (IsAbsolutePath(path))
            return path;
        if (string.IsNullOrEmpty(dir))
            return path;
        var combined = Path.Combine(dir, path);
        if (!Path.IsPathRooted(combined))
            combined = Path.Combine(Directory.GetCurrentDirectory(), combined);
        else if (!Path.IsPathFullyQualified(combined))
            combined = Path.GetFullPath(combined);
        return NormalizeSeparators(combined);
    }

    static string NormalizeSeparators(string path)
    {
        char sep = Path.DirectorySeparatorChar;
        var sb = new StringBuilder(path.Length);
        foreach (char c in path)
        {
            char ch = (c == '/' || c == '\\') && sep == '\\' ? '\\' : c;
            // 続く区切り文字は 1 つにする (UNC の先頭は残す)
            if (ch == sep && sb.Length > 1 && sb[^1] == sep)
                continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>FileUtil.load(path, true): 読めなければメッセージを出して null。</summary>
    public static byte[] Load(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FileUtil.load('" + path + "') error :");
            Console.Error.WriteLine(e.Message);
            return null;
        }
    }

    /// <summary>ArrayUtil.shortToByte(data): ビッグエンディアン。</summary>
    public static byte[] ShortToByte(short[] data)
    {
        var result = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            result[2 * i] = (byte)(data[i] >> 8);
            result[2 * i + 1] = (byte)data[i];
        }
        return result;
    }

    /// <summary>ArrayUtil.intToByte(data): ビッグエンディアン。</summary>
    public static byte[] IntToByte(int[] data)
    {
        var result = new byte[data.Length * 4];
        for (int i = 0; i < data.Length; i++)
        {
            result[4 * i] = (byte)(data[i] >> 24);
            result[4 * i + 1] = (byte)(data[i] >> 16);
            result[4 * i + 2] = (byte)(data[i] >> 8);
            result[4 * i + 3] = (byte)data[i];
        }
        return result;
    }
}
