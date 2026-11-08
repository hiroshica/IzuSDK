using System.Text;

namespace Isdk.Rescomp;

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
    public static int ParseInt(string s, int def) => TryParseInt(s, out int v) ? v : def;

    static bool TryParseInt(string s, out int value)
    {
        value = 0;
        if (s == null || s.Length == 0)
            return false;
        int i = 0;
        bool neg = false;
        if (s[0] == '-' || s[0] == '+')
        {
            neg = s[0] == '-';
            i = 1;
            if (s.Length == 1)
                return false;
        }
        long v = 0;
        for (; i < s.Length; i++)
        {
            int d = s[i] - '0';
            if (d < 0 || d > 9)
                return false;
            v = v * 10 + d;
            if (v > (neg ? 2147483648L : int.MaxValue))
                return false;
        }
        value = (int)(neg ? -v : v);
        return true;
    }

    /// <summary>Double.parseDouble に近い読み方 (前後の空白は無視、末尾の d / f も許す)。</summary>
    public static bool TryParseDouble(string s, out double value)
    {
        value = 0;
        if (s == null) return false;
        s = s.Trim();
        if (s.Length > 1 && "dDfF".IndexOf(s[^1]) >= 0) s = s[..^1];
        if (s.Length == 0) return false;
        return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
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
    public static List<string> JavaHashSetOrder(IReadOnlyList<string> items) =>
        JavaHashSetOrder(items, HashCode, (a, b) => a == b);

    /// <summary>
    /// Java の HashSet に items を順に add したときの、取り出しの順番 (重複は除く)。
    /// 入れ物の数は 16 から始まり、要素の数が 3/4 を超えるか、入れ物が 64 未満で 1 つに 9 個以上入ると倍になる。
    /// 同じ入れ物の中は入れた順 (木構造になる場合はまねていない)。
    /// </summary>
    public static List<T> JavaHashSetOrder<T>(IReadOnlyList<T> items, Func<T, int> hash, Func<T, T, bool> equal)
    {
        var kept = new List<(T Item, int Hash)>();
        int cap = 16;
        foreach (var item in items)
        {
            int h = Spread(hash(item));
            if (kept.Any(k => k.Hash == h && equal(k.Item, item)))
                continue;
            int chain = kept.Count(k => (k.Hash & (cap - 1)) == (h & (cap - 1)));
            kept.Add((item, h));
            if (chain >= 8 && cap < 64)
                cap *= 2;
            if (kept.Count > cap * 3 / 4)
                cap *= 2;
        }
        return kept
            .Select((k, i) => (k.Item, i, b: k.Hash & (cap - 1)))
            .OrderBy(t => t.b).ThenBy(t => t.i)
            .Select(t => t.Item)
            .ToList();
    }

    static int Spread(int h) => h ^ (int)((uint)h >> 16);

    /// <summary>java.util.List.hashCode と同じ。</summary>
    public static int ListHashCode<T>(IEnumerable<T> list)
    {
        int h = 1;
        foreach (var e in list)
            h = unchecked(31 * h + (e == null ? 0 : e.GetHashCode()));
        return h;
    }

    /// <summary>Integer.parseInt と同じ (数字でなければ FormatException)。</summary>
    public static int ParseIntStrict(string s) =>
        TryParseInt(s, out int v) ? v : throw new FormatException("For input string: \"" + s + "\"");

    /// <summary>StringUtil.isTaggedNumber: "125P" のように数字のあとに tag が続く。</summary>
    public static bool IsTaggedNumber(string s, string tag) =>
        !string.IsNullOrEmpty(s?.Trim()) && !string.IsNullOrEmpty(tag?.Trim()) &&
        System.Text.RegularExpressions.Regex.IsMatch(s, "^[0-9]+" + System.Text.RegularExpressions.Regex.Escape(tag) + "$");

    public static int ParseTaggedInt(string s, string tag, int def) =>
        IsTaggedNumber(s, tag) ? ParseIntStrict(s.Substring(0, s.Length - tag.Length)) : def;

    /// <summary>StringUtil.parseIntArray: "[1,2,3]" か "5"。読めなければ def。</summary>
    public static int[] ParseIntArray(string s, int[] def)
    {
        if (s == null)
            return def;
        try
        {
            if (s[0] == '[')
            {
                var parts = SplitJava(s.Substring(1, s.Length - 2), ",");
                return parts.Select(p => ParseIntStrict(Trim(p))).ToArray();
            }
            return new[] { ParseIntStrict(Trim(s)) };
        }
        catch (FormatException)
        {
            return def;
        }
    }

    /// <summary>StringUtil.parseIntArray2D: "[[1,2],[3]]" か "5"。読めなければ def。</summary>
    public static int[][] ParseIntArray2D(string s, int[][] def)
    {
        if (s == null)
            return def;
        try
        {
            if (s[0] == '[')
            {
                var ss = s.Substring(1, s.Length - 2).Replace("[", "");
                var arrayStr = SplitJava(ss, "]");
                var result = new int[arrayStr.Length][];
                for (int i = 0; i < arrayStr.Length; i++)
                {
                    var array1d = Trim(arrayStr[i]);
                    if (array1d.Length == 0)
                        throw new ArgumentOutOfRangeException(nameof(s), "String index out of range: 0");
                    if (array1d[0] == ',')
                        array1d = array1d.Substring(1);
                    result[i] = ParseIntArray("[" + array1d + "]", def.Length > i ? def[i] : new[] { 0 });
                }
                return result;
            }
            return new[] { new[] { ParseIntStrict(s) } };
        }
        catch (FormatException)
        {
            return def;
        }
    }

    /// <summary>String.split(固定の文字列) と同じ (末尾の空の要素は捨てる)。</summary>
    public static string[] SplitJava(string s, string sep)
    {
        var parts = s.Split(sep).ToList();
        while (parts.Count > 1 && parts[^1].Length == 0)
            parts.RemoveAt(parts.Count - 1);
        if (parts.Count == 1 && parts[0].Length == 0 && s.Length > 0)
            parts.Clear();
        return parts.ToArray();
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

    /// <summary>ArrayUtil.byteToInt(data): ビッグエンディアン (端数のバイトは捨てる)。</summary>
    public static int[] ByteToInt(byte[] data)
    {
        var result = new int[data.Length / 4];
        for (int i = 0; i < result.Length; i++)
            result[i] = (data[4 * i] << 24) | (data[4 * i + 1] << 16) | (data[4 * i + 2] << 8) | data[4 * i + 3];
        return result;
    }

    /// <summary>ArrayUtil.byteToShort(data): ビッグエンディアン。</summary>
    public static short[] ByteToShort(byte[] data)
    {
        var result = new short[data.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = (short)((data[2 * i] << 8) | data[2 * i + 1]);
        return result;
    }
}
