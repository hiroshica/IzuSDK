namespace Sgdk.Rescomp.Types;

/// <summary>
/// java.awt.Rectangle と同じ計算をする矩形 (交差で幅や高さが負になるところまで同じ)。
/// スプライトの分割の結果を Java 版と合わせるため、equals / hashCode も Java と同じにする。
/// </summary>
public class JRect
{
    public int X, Y, Width, Height;

    public JRect() { }

    public JRect(int x, int y, int w, int h)
    {
        X = x; Y = y; Width = w; Height = h;
    }

    public JRect(int w, int h) : this(0, 0, w, h) { }

    public JRect(JRect r) : this(r.X, r.Y, r.Width, r.Height) { }

    public bool IsEmpty => Width <= 0 || Height <= 0;
    public double MaxX => X + (double)Width;
    public double MaxY => Y + (double)Height;

    public JRect Intersection(JRect r)
    {
        int tx1 = X, ty1 = Y, rx1 = r.X, ry1 = r.Y;
        long tx2 = tx1 + (long)Width, ty2 = ty1 + (long)Height;
        long rx2 = rx1 + (long)r.Width, ry2 = ry1 + (long)r.Height;
        if (tx1 < rx1) tx1 = rx1;
        if (ty1 < ry1) ty1 = ry1;
        if (tx2 > rx2) tx2 = rx2;
        if (ty2 > ry2) ty2 = ry2;
        tx2 -= tx1;
        ty2 -= ty1;
        if (tx2 < int.MinValue) tx2 = int.MinValue;
        if (ty2 < int.MinValue) ty2 = int.MinValue;
        return new JRect(tx1, ty1, (int)tx2, (int)ty2);
    }

    public bool Intersects(JRect r)
    {
        int tw = Width, th = Height, rw = r.Width, rh = r.Height;
        if (rw <= 0 || rh <= 0 || tw <= 0 || th <= 0)
            return false;
        int tx = X, ty = Y, rx = r.X, ry = r.Y;
        rw += rx; rh += ry; tw += tx; th += ty;
        return (rw < rx || rw > tx) && (rh < ry || rh > ty) && (tw < tx || tw > rx) && (th < ty || th > ry);
    }

    public bool Contains(JRect r)
    {
        int w = Width, h = Height, W = r.Width, H = r.Height;
        if ((w | h | W | H) < 0)
            return false;
        int x = X, y = Y, rx = r.X, ry = r.Y;
        if (rx < x || ry < y)
            return false;
        w += x; W += rx;
        if (W <= rx) { if (w >= x || W > w) return false; }
        else { if (w >= x && W > w) return false; }
        h += y; H += ry;
        if (H <= ry) { if (h >= y || H > h) return false; }
        else { if (h >= y && H > h) return false; }
        return true;
    }

    /// <summary>Rectangle.add(Rectangle): 両方を囲む矩形にする。</summary>
    public void Add(JRect r)
    {
        long tx2 = Width, ty2 = Height;
        if ((tx2 | ty2) < 0)
        {
            X = r.X; Y = r.Y; Width = r.Width; Height = r.Height;
        }
        long rx2 = r.Width, ry2 = r.Height;
        if ((rx2 | ry2) < 0)
            return;
        int tx1 = X, ty1 = Y;
        tx2 += tx1; ty2 += ty1;
        int rx1 = r.X, ry1 = r.Y;
        rx2 += rx1; ry2 += ry1;
        if (tx1 > rx1) tx1 = rx1;
        if (ty1 > ry1) ty1 = ry1;
        if (tx2 < rx2) tx2 = rx2;
        if (ty2 < ry2) ty2 = ry2;
        tx2 -= tx1; ty2 -= ty1;
        if (tx2 > int.MaxValue) tx2 = int.MaxValue;
        if (ty2 > int.MaxValue) ty2 = int.MaxValue;
        X = tx1; Y = ty1; Width = (int)tx2; Height = (int)ty2;
    }

    public bool SameLocation(int x, int y) => X == x && Y == y;

    /// <summary>Rectangle.equals: 位置と大きさが同じ。</summary>
    public override bool Equals(object obj) =>
        obj is JRect r ? X == r.X && Y == r.Y && Width == r.Width && Height == r.Height : false;

    /// <summary>Rectangle2D.hashCode と同じ。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            long bits = BitConverter.DoubleToInt64Bits(X);
            bits += BitConverter.DoubleToInt64Bits(Y) * 37;
            bits += BitConverter.DoubleToInt64Bits(Width) * 43;
            bits += BitConverter.DoubleToInt64Bits(Height) * 47;
            return (int)bits ^ (int)(bits >> 32);
        }
    }

    public override string ToString() => "[x=" + X + ",y=" + Y + ",width=" + Width + ",height=" + Height + "]";
}

/// <summary>
/// java.awt.geom.Area のうち、整数の矩形を足し合わせたものだけを扱う (画素の集まりとして持つ)。
/// </summary>
public sealed class PixelArea
{
    readonly HashSet<long> pixels = new();

    static long Key(int x, int y) => ((long)x << 32) | (uint)y;

    public void Add(JRect r)
    {
        for (int y = r.Y; y < r.Y + r.Height; y++)
            for (int x = r.X; x < r.X + r.Width; x++)
                pixels.Add(Key(x, y));
    }

    public void Intersect(JRect r) =>
        pixels.RemoveWhere(k =>
        {
            int x = (int)(k >> 32), y = (int)k;
            return x < r.X || y < r.Y || x >= r.X + r.Width || y >= r.Y + r.Height;
        });

    public bool IsEmpty => pixels.Count == 0;

    public bool Intersects(JRect r)
    {
        if (r.Width <= 0 || r.Height <= 0)
            return false;
        for (int y = r.Y; y < r.Y + r.Height; y++)
            for (int x = r.X; x < r.X + r.Width; x++)
                if (pixels.Contains(Key(x, y)))
                    return true;
        return false;
    }

    public bool Contains(JRect r)
    {
        if (r.Width <= 0 || r.Height <= 0)
            return false;
        for (int y = r.Y; y < r.Y + r.Height; y++)
            for (int x = r.X; x < r.X + r.Width; x++)
                if (!pixels.Contains(Key(x, y)))
                    return false;
        return true;
    }
}
