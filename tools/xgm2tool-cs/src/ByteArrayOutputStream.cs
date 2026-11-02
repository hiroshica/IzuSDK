namespace Sgdk.Xgm2Tool;

/// <summary>java.io.ByteArrayOutputStream の最小互換実装。</summary>
public class ByteArrayOutputStream
{
    protected byte[] buf;
    protected int count;

    public ByteArrayOutputStream() : this(32) { }
    public ByteArrayOutputStream(int size) { buf = new byte[Math.Max(size, 1)]; }

    void Ensure(int n)
    {
        if (n > buf.Length) Array.Resize(ref buf, Math.Max(buf.Length * 2, n));
    }

    public void write(int b) { Ensure(count + 1); buf[count++] = (byte)b; }
    public void write(byte[] b) { write(b, 0, b.Length); }
    public void write(byte[] b, int off, int len)
    {
        Ensure(count + len);
        Array.Copy(b, off, buf, count, len);
        count += len;
    }
    public int size() => count;
    public void reset() { count = 0; }
    public byte[] toByteArray() { var r = new byte[count]; Array.Copy(buf, r, count); return r; }
}

/// <summary>
/// Java HashMap の代用。キーの昇順で列挙する (整数の小さなキーでは Java の HashMap と同じ順序)。
/// 存在しないキーの get は null/0 を返す。
/// </summary>
public class JMap<K, V> : IEnumerable<KeyValuePair<K, V>>
{
    readonly SortedDictionary<K, V> d = new();
    public V this[K k]
    {
        get { return d.TryGetValue(k, out var v) ? v : default; }
        set { d[k] = value; }
    }
    public V get(K k) => this[k];
    public void put(K k, V v) { d[k] = v; }
    public bool containsKey(K k) => d.ContainsKey(k);
    public bool Remove(K k) => d.Remove(k);
    public void Clear() => d.Clear();
    public int Count => d.Count;
    public IEnumerable<K> Keys => d.Keys;
    public IEnumerable<V> Values => d.Values;
    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => d.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => d.GetEnumerator();
}
