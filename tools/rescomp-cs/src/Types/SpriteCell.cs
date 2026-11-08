using Isdk.Rescomp.Imaging;

namespace Isdk.Rescomp.Types;

public enum OptimizationType { BALANCED, MIN_SPRITE, MIN_TILE, NONE }

public enum OptimizationLevel { FAST, MEDIUM, SLOW, MAX }

/// <summary>Java 版の sgdk.tool.Random (種を決めない乱数)。</summary>
public static class JRandom
{
    static readonly Random generator = new();

    public static int NextInt() => generator.Next(int.MinValue, int.MaxValue);
    public static int NextInt(int n) => n <= 0 ? throw new ArgumentException("bound must be positive") : generator.Next(n);
    public static bool NextBoolean() => generator.Next(2) == 1;
}

/// <summary>スプライトの分割の 1 つの区画 (Java 版の type.SpriteCell)。</summary>
public sealed class SpriteCell : JRect, IComparable<SpriteCell>
{
    /// <summary>タイルの数の多い順 (Collections.sort と同じく安定に並べること)。</summary>
    public static readonly Comparison<SpriteCell> SizeComparator = (o1, o2) => -o1.NumTile.CompareTo(o2.NumTile);

    public readonly OptimizationType Opt;
    public readonly int NumTile;
    public int CoveredPix;

    public SpriteCell(JRect r, OptimizationType opt) : this(r.X, r.Y, r.Width, r.Height, opt) { }

    public SpriteCell(int x, int y, int width, int height, OptimizationType opt) : base(x, y, width, height)
    {
        Opt = opt;
        NumTile = (width * height) / 64;
        CoveredPix = -1;
    }

    public bool IsSingleTile => Width == 8 && Height == 8;

    public List<SpriteCell> Mutate()
    {
        var result = new List<SpriteCell>(4);
        switch (JRandom.NextInt() & 7)
        {
            default:
            case 0: result.Add(MutateMove(1)); break;
            case 1: result.Add(MutateMove(4)); break;
            case 2: result.Add(MutateSize(false)); break;
            case 3: result.Add(MutateSize(true)); break;
            case 4: result.Add(MutateMove(1).MutateSize(false)); break;
            case 5: result.Add(MutateMove(4).MutateSize(true)); break;
            case 6: result.AddRange(MutateSplit()); break;
            case 7: result.AddRange(MutateMove(1).MutateSplit()); break;
        }
        return result;
    }

    SpriteCell MutateMove(int move)
    {
        var r = new JRect(this);
        switch (JRandom.NextInt() & 3)
        {
            default:
            case 0: r.X += move; break;
            case 1: r.X -= move; break;
            case 2: r.Y += move; break;
            case 3: r.Y -= move; break;
        }
        return new SpriteCell(r, Opt);
    }

    SpriteCell MutateSize(bool multi)
    {
        var r = new JRect(this);
        int it = 1;
        if (multi)
            it += JRandom.NextInt() & 3;
        while (it > 0)
        {
            switch (JRandom.NextInt() & 3)
            {
                default:
                case 0: if (r.Width < 32) { r.Width += 8; it--; } break;
                case 1: if (r.Width > 8) { r.Width -= 8; it--; } break;
                case 2: if (r.Height < 32) { r.Height += 8; it--; } break;
                case 3: if (r.Height > 8) { r.Height -= 8; it--; } break;
            }
        }
        return new SpriteCell(r, Opt);
    }

    List<SpriteCell> MutateSplit()
    {
        var result = new List<SpriteCell>(4);
        int sw = JRandom.NextInt(Width / 8) * 8;
        int sh = JRandom.NextInt(Height / 8) * 8;
        var r1 = new JRect(X, Y, sw, sh);
        var r2 = new JRect(X + sw, Y, Width - sw, sh);
        var r3 = new JRect(X, Y + sh, sw, Height - sh);
        var r4 = new JRect(X + sw, Y + sh, Width - sw, Height - sh);
        if (!r1.IsEmpty) result.Add(new SpriteCell(r1, Opt));
        if (!r2.IsEmpty) result.Add(new SpriteCell(r2, Opt));
        if (!r3.IsEmpty) result.Add(new SpriteCell(r3, Opt));
        if (!r4.IsEmpty) result.Add(new SpriteCell(r4, Opt));
        return result;
    }

    public double GetScore() => GetBaseScore();

    public double GetBaseScore() => Opt switch
    {
        OptimizationType.MIN_SPRITE => 12d + (NumTile * 1d) + (Width / 32d),
        OptimizationType.MIN_TILE => 6d + (NumTile * 4d) + (Width / 32d),
        _ => 8d + (NumTile * 2.5d) + (Width / 32d),
    };

    /// <summary>重なりの面積 (Java と同じく、重ならないと幅と高さが負になって正の値になることがある)。</summary>
    public int GetOverdraw(SpriteCell cell)
    {
        var r = Intersection(cell);
        return unchecked(r.Height * r.Width);
    }

    public double GetCoverage() => CoveredPix == -1 ? 0d : (double)CoveredPix / (NumTile * 64);

    /// <summary>ほかの区画と重なっている部分を減らすように位置をずらす (この区画を書き換える)。</summary>
    public bool OptimizeOverdraw(byte[] image, int imgW, int imgH, List<SpriteCell> allSpr)
    {
        var area = new PixelArea();
        foreach (var sc in allSpr)
            if (!ReferenceEquals(sc, this))
                area.Add(sc);
        area.Intersect(this);

        if (area.IsEmpty)
            return false;

        int initX = X, initY = Y;
        var rectH = new JRect(0, 0, Width, 1);
        var rectV = new JRect(0, 0, 1, Height);

        while (CanMoveLeft(image, imgW, imgH, area, X, Y, rectH, rectV))
        {
            X--;
            if (X < 0) { X++; break; }
        }
        while (CanMoveRight(image, imgW, imgH, area, X, Y, rectH, rectV))
        {
            X++;
            if (X + Width > imgW) { X--; break; }
        }
        while (CanMoveTop(image, imgW, imgH, area, X, Y, rectH, rectV))
        {
            Y--;
            if (Y < 0) { Y++; break; }
        }
        while (CanMoveBottom(image, imgW, imgH, area, X, Y, rectH, rectV))
        {
            Y++;
            if (Y + Height > imgH) { Y--; break; }
        }

        return !SameLocation(initX, initY);
    }

    public int CompareTo(SpriteCell o) => GetScore().CompareTo(o.GetScore());

    public override string ToString() => "[" + X + "," + Y + "-" + Width + "," + Height + "] cov= " + (int)(GetCoverage() * 100d) + "%";

    public static SpriteCell OptimizePosition(byte[] image, int imgW, int imgH, SpriteCell spr)
    {
        var bounds = new JRect(imgW, imgH);
        var region = new JRect(spr);
        while (!ImageUtil.HasOpaquePixelOnEdge(image, imgW, imgH, region, false, false, true, false) && region.Intersects(bounds))
            region.X--;
        while (!ImageUtil.HasOpaquePixelOnEdge(image, imgW, imgH, region, false, false, false, true) && region.Intersects(bounds))
            region.Y--;
        while (!ImageUtil.HasOpaquePixelOnEdge(image, imgW, imgH, region, true, false, false, false) && region.Intersects(bounds))
            region.X++;
        while (!ImageUtil.HasOpaquePixelOnEdge(image, imgW, imgH, region, false, true, false, false) && region.Intersects(bounds))
            region.Y++;
        return new SpriteCell(region, spr.Opt);
    }

    public static SpriteCell OptimizeSize(byte[] image, int imgW, int imgH, SpriteCell spr)
    {
        var region = new JRect(spr);
        int covered = ImageUtil.GetOpaquePixelCount(image, imgW, imgH, region);
        if (covered == 0)
            return null;

        do { region.X += 8; region.Width -= 8; }
        while (region.Width > 0 && ImageUtil.GetOpaquePixelCount(image, imgW, imgH, region) == covered);
        region.X -= 8; region.Width += 8;

        do { region.Y += 8; region.Height -= 8; }
        while (region.Height > 0 && ImageUtil.GetOpaquePixelCount(image, imgW, imgH, region) == covered);
        region.Y -= 8; region.Height += 8;

        do region.Width -= 8;
        while (region.Width > 0 && ImageUtil.GetOpaquePixelCount(image, imgW, imgH, region) == covered);
        region.Width += 8;

        do region.Height -= 8;
        while (region.Height > 0 && ImageUtil.GetOpaquePixelCount(image, imgW, imgH, region) == covered);
        region.Height += 8;

        return new SpriteCell(region, spr.Opt);
    }

    static bool CanMoveTop(byte[] image, int imgW, int imgH, PixelArea area, int x, int y, JRect rectH, JRect rectV)
    {
        rectH.X = x; rectH.Y = y + (rectV.Height - 1);
        if (area.Intersects(rectH))
        {
            var opaque = ImageUtil.GetOpaqueRect(image, imgW, imgH, rectH);
            if (opaque.IsEmpty || area.Contains(opaque))
            {
                rectH.X = x; rectH.Y = y - 1;
                if (!area.Intersects(rectH))
                    return true;
            }
        }
        return false;
    }

    static bool CanMoveBottom(byte[] image, int imgW, int imgH, PixelArea area, int x, int y, JRect rectH, JRect rectV)
    {
        rectH.X = x; rectH.Y = y;
        if (area.Intersects(rectH))
        {
            var opaque = ImageUtil.GetOpaqueRect(image, imgW, imgH, rectH);
            if (opaque.IsEmpty || area.Contains(opaque))
            {
                rectH.X = x; rectH.Y = y + rectV.Height;
                if (!area.Intersects(rectH))
                    return true;
            }
        }
        return false;
    }

    static bool CanMoveLeft(byte[] image, int imgW, int imgH, PixelArea area, int x, int y, JRect rectH, JRect rectV)
    {
        rectV.X = x + (rectH.Width - 1); rectV.Y = y;
        if (area.Intersects(rectV))
        {
            var opaque = ImageUtil.GetOpaqueRect(image, imgW, imgH, rectV);
            if (opaque.IsEmpty || area.Contains(opaque))
            {
                rectV.X = x - 1; rectV.Y = y;
                if (!area.Intersects(rectV))
                    return true;
            }
        }
        return false;
    }

    static bool CanMoveRight(byte[] image, int imgW, int imgH, PixelArea area, int x, int y, JRect rectH, JRect rectV)
    {
        rectV.X = x; rectV.Y = y;
        if (area.Intersects(rectV))
        {
            var opaque = ImageUtil.GetOpaqueRect(image, imgW, imgH, rectV);
            if (opaque.IsEmpty || area.Contains(opaque))
            {
                rectV.X = x + rectH.Width; rectV.Y = y;
                if (!area.Intersects(rectV))
                    return true;
            }
        }
        return false;
    }

    /// <summary>区画を画像の中に収める (負の位置にしない)。</summary>
    public static SpriteCell FixPosition(int imgW, int imgH, SpriteCell spr)
    {
        var region = new JRect(spr);
        if (region.MaxX > imgW) region.X -= (int)(region.MaxX - imgW);
        if (region.MaxY > imgH) region.Y -= (int)(region.MaxY - imgH);
        if (region.X < 0) region.X = 0;
        if (region.Y < 0) region.Y = 0;
        return new SpriteCell(region, spr.Opt);
    }
}

/// <summary>タイルの格子 (Java 版の type.CellGrid)。</summary>
public sealed class CellGrid
{
    readonly SpriteCell[][] grid;
    readonly int offsetX;
    readonly int offsetY;

    public CellGrid(int offsetX, int offsetY, int sx, int sy)
    {
        grid = new SpriteCell[sy][];
        for (int j = 0; j < sy; j++)
            grid[j] = new SpriteCell[sx];
        this.offsetX = offsetX;
        this.offsetY = offsetY;
    }

    public void Set(int x, int y, SpriteCell value) => grid[y][x] = value;

    public bool IsUsed(int x, int y, bool allowFused)
    {
        if (x < 0 || y < 0)
            return false;
        if (y >= grid.Length || x >= grid[0].Length)
            return false;
        return grid[y][x] != null && (allowFused || grid[y][x].IsSingleTile);
    }

    public int GetUsedCells(bool singleTileOnly) => GetUsedCells(0, 0, grid[0].Length, grid.Length, singleTileOnly);

    public int GetUsedCells(int x, int y, int w, int h, bool singleTileOnly)
    {
        int result = 0;
        for (int j = y; j < y + h; j++)
        {
            if (j >= grid.Length)
                return 0;
            var row = grid[j];
            for (int i = x; i < x + w; i++)
            {
                if (i >= row.Length)
                    return 0;
                var cell = row[i];
                if (cell != null && (!singleTileOnly || cell.IsSingleTile))
                    result++;
            }
        }
        return result;
    }

    public void MergeCells(OptimizationType opt)
    {
        for (int j = 0; j < grid.Length; j++)
        {
            var row = grid[j];
            for (int i = 0; i < row.Length; i++)
                if (row[i] != null && row[i].IsSingleTile)
                    MergeAt(i, j, opt);
        }
    }

    /// <summary>区画の一覧。Java 版は HashSet に入れて取り出すので、その順番をまねる。</summary>
    public List<SpriteCell> GetSpriteCells()
    {
        var all = new List<SpriteCell>();
        foreach (var row in grid)
            foreach (var c in row)
                if (c != null)
                    all.Add(c);
        return JavaCompat.JavaHashSetOrder(all, c => c.GetHashCode(), (a, b) => a.Equals(b));
    }

    void MergeAt(int x, int y, OptimizationType opt)
    {
        int w = 1;
        while (w < 4 && IsUsed(x + w, y, false))
            w++;

        int h = 1;
        while (h < 4)
        {
            bool ok = true;
            for (int i = 0; i < w; i++)
            {
                if (!IsUsed(x + i, y + h, false))
                {
                    ok = false;
                    break;
                }
            }
            if (!ok)
                break;
            if (w != 4 && (IsUsed(x - 1, y + h, false) || IsUsed(x + w, y + h, false)))
                break;
            h++;
        }

        if (w > 1 || h > 1)
        {
            var cell = new SpriteCell(offsetX + (x * 8), offsetY + (y * 8), w * 8, h * 8, opt);
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    grid[j][i] = cell;
        }
    }
}
