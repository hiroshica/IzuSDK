using System.Globalization;
using System.Text;

namespace Isdk.Xgm2Tool;

/// <summary>Java の標準ライブラリと同じ動きをする小物。</summary>
public static class J
{
    public static string D2S(double d) => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    public static T remove<T>(this List<T> l, int i) { var o = l[i]; l.RemoveAt(i); return o; }
    public static bool remove<T>(this List<T> l, T item) where T : class => l.Remove(item);
    public static T set<T>(this List<T> l, int i, T v) { var o = l[i]; l[i] = v; return o; }
    public static void Add<T>(this List<T> l, int i, T v) => l.Insert(i, v);
    public static void AddRange<T>(this List<T> l, int i, IEnumerable<T> c) => l.InsertRange(i, c);

    public static T[][] Jagged<T>(int a, int b) { var r = new T[a][]; for (int i = 0; i < a; i++) r[i] = new T[b]; return r; }

    public static int Compare(int a, int b) => a < b ? -1 : (a > b ? 1 : 0);
    public static int Compare(long a, long b) => a < b ? -1 : (a > b ? 1 : 0);

    /// <summary>Math.round: floor(x + 0.5)。</summary>
    public static long Round(double x) => (long)Math.Floor(x + 0.5d);
    public static int Round(float x) => (int)Math.Floor(x + 0.5f);

    public static string ToHexString(int v) => ((uint)v).ToString("x");

    public static T[] CopyOfRange<T>(T[] a, int from, int to)
    {
        if (from > to) throw new ArgumentException(from + " > " + to);
        if (from < 0 || from > a.Length) throw new IndexOutOfRangeException();
        var r = new T[to - from];
        Array.Copy(a, from, r, 0, Math.Min(a.Length - from, r.Length));
        return r;
    }

    public static T[] CopyOf<T>(T[] a, int len)
    {
        var r = new T[len];
        Array.Copy(a, r, Math.Min(a.Length, len));
        return r;
    }

    public static bool ArrayEquals<T>(T[] a, T[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
        return true;
    }

    public static void Fill<T>(T[] a, T v) => Array.Fill(a, v);
    public static void Fill<T>(T[] a, int from, int to, T v) => Array.Fill(a, v, from, to - from);

    public static int HashCode(int[] a)
    {
        if (a == null) return 0;
        int h = 1;
        foreach (var x in a) h = unchecked(31 * h + x);
        return h;
    }

    public static int HashCode(byte[] a)
    {
        if (a == null) return 0;
        int h = 1;
        foreach (var x in a) h = unchecked(31 * h + (sbyte)x);
        return h;
    }

    /// <summary>Collections.sort: 安定な並べ替え。</summary>
    public static void Sort<T>(List<T> list, Comparison<T> cmp)
    {
        var sorted = list.Select((v, i) => (v, i)).OrderBy(p => p, Comparer<(T v, int i)>.Create((a, b) =>
        {
            int r = cmp(a.v, b.v);
            return r != 0 ? r : a.i.CompareTo(b.i);
        })).Select(p => p.v).ToList();
        list.Clear();
        list.AddRange(sorted);
    }

    public static void Sort<T>(List<T> list) where T : IComparable<T> => Sort(list, (a, b) => a.CompareTo(b));
    public static void Sort<T>(List<T> list, IComparer<T> cmp) => Sort(list, (a, b) => cmp.Compare(a, b));

    public static void Reverse<T>(List<T> list) => list.Reverse();

    public static bool IsEmptyJ(this string s) => s.Length == 0;
    public static bool IsEmptyJ<T>(this ICollection<T> c) => c.Count == 0;
        public static bool EqualsIgnoreCaseJ(this string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>List.remove(Object)</summary>
    public static bool RemoveObj<T>(this List<T> l, T item) => l.Remove(item);

    /// <summary>List.set(i, v) が返す前の値</summary>
    public static T SetJ<T>(this List<T> l, int i, T v) { var o = l[i]; l[i] = v; return o; }

    /// <summary>Java の String.format (使われている %d %s %X %02X %6X %f などだけ)。</summary>
    public static string Format(string fmt, params object[] args)
    {
        var sb = new StringBuilder();
        int ai = 0;
        for (int i = 0; i < fmt.Length; i++)
        {
            char c = fmt[i];
            if (c != '%') { sb.Append(c); continue; }
            i++;
            if (fmt[i] == '%') { sb.Append('%'); continue; }
            if (fmt[i] == 'n') { sb.Append('\n'); continue; }
            bool zero = false, left = false;
            while (fmt[i] == '0' || fmt[i] == '-' || fmt[i] == ' ' || fmt[i] == '+')
            {
                if (fmt[i] == '0') zero = true;
                if (fmt[i] == '-') left = true;
                i++;
            }
            int width = 0;
            while (char.IsDigit(fmt[i])) width = width * 10 + (fmt[i++] - '0');
            int prec = -1;
            if (fmt[i] == '.')
            {
                i++;
                prec = 0;
                while (char.IsDigit(fmt[i])) prec = prec * 10 + (fmt[i++] - '0');
            }
            char conv = fmt[i];
            object a = args[ai++];
            string s;
            switch (conv)
            {
                case 'd': s = Convert.ToInt64(a).ToString(CultureInfo.InvariantCulture); break;
                case 'x': s = a is long lx ? lx.ToString("x") : ((uint)Convert.ToInt32(a)).ToString("x"); break;
                case 'X': s = a is long lX ? lX.ToString("X") : ((uint)Convert.ToInt32(a)).ToString("X"); break;
                case 'f': s = Convert.ToDouble(a).ToString("F" + (prec < 0 ? 6 : prec), CultureInfo.InvariantCulture); break;
                default: s = a?.ToString() ?? "null"; break;
            }
            if (s.Length < width)
                s = left ? s.PadRight(width) : s.PadLeft(width, zero ? '0' : ' ');
            sb.Append(s);
        }
        return sb.ToString();
    }


    static int Spread(int h) => h ^ (int)((uint)h >> 16);

    /// <summary>
    /// Java の HashMap / HashSet にキーを順に入れたときの、取り出しの順番 (キーは重複のない、入れた順の列)。
    /// 入れ物の数は 16 から始まり、要素数が 3/4 を超えるか、入れ物が 64 未満で 1 つに 9 個以上入ると倍になる。
    /// 同じ入れ物の中は入れた順 (木構造になる場合はまねていない)。minCapacity は途中で消して縮んだ場合などの下限。
    /// </summary>
    public static List<K> HashOrder<K>(IReadOnlyList<K> keys, Func<K, int> hash, int minCapacity = 16)
    {
        var hashes = new List<int>();
        int cap = minCapacity;
        foreach (var k in keys)
        {
            int h = Spread(hash(k));
            int chain = hashes.Count(x => (x & (cap - 1)) == (h & (cap - 1)));
            hashes.Add(h);
            if (chain >= 8 && cap < 64) cap *= 2;
            if (hashes.Count > cap * 3 / 4) cap *= 2;
        }
        return keys.Select((k, i) => (k, i, b: hashes[i] & (cap - 1)))
            .OrderBy(t => t.b).ThenBy(t => t.i)
            .Select(t => t.k).ToList();
    }

    public static int HashOfLong(long v) => (int)(v ^ (long)((ulong)v >> 32));

    public static long NextPow2(long value)
    {
        long result;
        if (value < 0)
        {
            result = -1;
            while (result > value) result <<= 1;
            result >>= 1;
        }
        else
        {
            result = 1;
            while (result <= value) result <<= 1;
        }
        return result;
    }
}

/// <summary>sgdk.tool.StringUtil の使っている分だけ。</summary>
public static class StringUtil
{
    public static bool isEmpty(string v, bool trim) => v == null || (trim ? v.Trim().Length == 0 : v.Length == 0);
    public static bool isEmpty(string v) => isEmpty(v, true);

    public static string toHexaString(int value) => ((uint)value).ToString("x");
    public static string toHexaString(int value, int size)
    {
        string r = ((uint)value).ToString("x");
        if (r.Length > size) return r.Substring(r.Length - size);
        while (r.Length < size) r = "0" + r;
        return r;
    }

    public static bool equals(string a, string b)
    {
        if (isEmpty(a, false)) return isEmpty(b, false);
        if (isEmpty(b, false)) return false;
        return a == b;
    }
}

public static class MathUtil
{
    public static long nextPow2(long value) => J.NextPow2(value);
}
