using Sgdk.Rescomp.Imaging;
using Sgdk.Rescomp.Types;

namespace Sgdk.Rescomp.Tools;

/// <summary>
/// スプライトのフレームを VDP スプライト (最大 4x4 タイル) に分ける (Java 版の tool.SpriteCutter)。
/// FAST / MEDIUM は Java 版と同じ結果になる。SLOW / MAX の遺伝的アルゴリズムは Java 版も種を決めない乱数を使うので、
/// 結果は実行ごとに変わる (ここでは 1 つのスレッドで同じ手順を回す)。
/// </summary>
public sealed class SpriteCutter
{
    /// <summary>Collections.sort と同じ安定な並べ替え。</summary>
    internal static void StableSort<T>(List<T> list, Comparison<T> cmp)
    {
        var sorted = list.Select((v, i) => (v, i)).ToList();
        sorted.Sort((a, b) =>
        {
            int c = cmp(a.v, b.v);
            return c != 0 ? c : a.i.CompareTo(b.i);
        });
        for (int i = 0; i < list.Count; i++)
            list[i] = sorted[i].v;
    }

    static List<Solution> GetFastOptimizedSolutions(byte[] image8bpp, int imgW, int imgH, OptimizationType optType, bool betterOpt)
    {
        var result = new List<Solution>();
        var cutter = new SpriteCutter(image8bpp, imgW, imgH);

        if (optType == OptimizationType.NONE)
            result.Add(cutter.GetDefaultSolution());
        else
        {
            // 格子の大きさは 8 と 32 だけでよい (8 のときは隣のタイルをつなげる)
            for (int gridSize = 8; gridSize <= 32; gridSize += 24)
            {
                var grids = betterOpt ? cutter.GetBestGrids(gridSize, optType) : new List<CellGrid> { cutter.GetBestGrid(gridSize, optType) };

                foreach (var grid in grids)
                {
                    try
                    {
                        if (gridSize == 8)
                            grid.MergeCells(optType);
                        var solution = cutter.GetSolution(grid, optType);
                        solution.FastOptimize();
                        if (solution.Cells.Count > 0)
                            result.Add(solution);
                    }
                    catch (Exception)
                    {
                        // Java 版と同じく、まれに起きるエラーはその解を捨てるだけ
                    }
                }
            }
        }
        return result;
    }

    public static List<SpriteCell> GetFastOptimizedSpriteList(byte[] image8bpp, int imgW, int imgH, OptimizationType optType, bool betterOpt)
    {
        var solutions = GetFastOptimizedSolutions(image8bpp, imgW, imgH, optType, betterOpt);
        if (solutions.Count == 0)
            return new List<SpriteCell>();

        StableSort(solutions, (a, b) => a.CompareTo(b));

        List<SpriteCell> result = null;
        foreach (var s in solutions)
        {
            var cells = s.Cells;
            if (cells.Count <= 16)
                return cells;
            if (result == null || cells.Count < result.Count)
                result = cells;
        }
        return result;
    }

    public static List<SpriteCell> GetSlowOptimizedSpriteList(byte[] image8bpp, int imgW, int imgH, long optIteration, OptimizationType optType)
    {
        var baseSolutions = new List<Solution>();
        var cutter = new SpriteCutter(image8bpp, imgW, imgH);

        baseSolutions.Add(cutter.GetDefaultSolution());

        for (int opt = 0; opt < 2; opt++)
        {
            for (int gridSize = 8; gridSize <= 32; gridSize += 24)
            {
                foreach (var grid in cutter.GetBestGrids(gridSize, optType))
                {
                    try
                    {
                        if (gridSize == 8)
                            grid.MergeCells(optType);
                        var solution = cutter.GetSolution(grid, optType);
                        if (opt == 1)
                            solution.FastOptimize();
                        solution.FixPos();
                        if (solution.Cells.Count > 0)
                            baseSolutions.Add(solution);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        var start = DateTime.UtcNow;
        var optimizer = new SolutionOptimizer(cutter, baseSolutions, optIteration);
        long it = optimizer.Run();
        long time = Math.Max(1, (long)(DateTime.UtcNow - start).TotalMilliseconds);
        Console.WriteLine(it + " iterations in " + time + " ms (" + ((it * 1000) / time) + " it/s)");

        var best = optimizer.GetBestSolution();
        best.FixPos();
        return best.Cells;
    }

    internal sealed class Solution : IComparable<Solution>
    {
        readonly SpriteCutter cutter;
        public readonly List<SpriteCell> Cells = new();
        public readonly byte[] CoverageImage;
        int remainingPixToCover;
        public int NumTile;
        double cachedScore;

        byte[] Image => cutter.image;
        int W => cutter.w;
        int H => cutter.h;

        public Solution(SpriteCutter cutter, byte[] coverageImage)
        {
            this.cutter = cutter;
            CoverageImage = coverageImage;
            Reset();
        }

        public Solution(SpriteCutter cutter) : this(cutter, new byte[cutter.image.Length]) { }

        public Solution(Solution solution) : this(solution.cutter)
        {
            foreach (var cell in solution.Cells)
                AddCell(new SpriteCell(cell, cell.Opt));
        }

        public Solution(SpriteCutter cutter, IEnumerable<JRect> sprites, OptimizationType opt) : this(cutter)
        {
            foreach (var r in sprites)
                AddCell(new SpriteCell(r, opt));
        }

        public void Reset()
        {
            Array.Copy(Image, CoverageImage, W * H);
            Cells.Clear();
            remainingPixToCover = cutter.numOpaquePixel;
            NumTile = 0;
            cachedScore = -1d;
        }

        public bool AddCell(SpriteCell cell)
        {
            var adj = cell.Intersection(new JRect(W, H));
            int w = adj.Width, h = adj.Height;
            var data = CoverageImage;
            int pitch = W;
            int offset = (adj.Y * pitch) + adj.X;
            int covered = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (data[offset] != 0)
                    {
                        covered++;
                        data[offset] = 0;
                    }
                    offset++;
                }
                offset += pitch - w;
            }

            if (covered > 0 || cell.Opt == OptimizationType.NONE)
            {
                remainingPixToCover -= covered;
                cell.CoveredPix = covered;
                NumTile += cell.NumTile;
                Cells.Add(cell);
                // Java 版はここでスコアのキャッシュを消さない (reset でだけ消す)
                return true;
            }
            return false;
        }

        public bool IsComplete => remainingPixToCover <= 0;

        int GetSpriteOverdraw()
        {
            int result = 0;
            foreach (var cur in Cells)
                foreach (var other in Cells)
                    if (!ReferenceEquals(cur, other))
                        result = unchecked(result + cur.GetOverdraw(other));
            return result;
        }

        public double GetScore()
        {
            if (!IsComplete)
                return double.MaxValue;

            if (cachedScore == -1d)
            {
                double result = 0d;
                foreach (var cell in Cells)
                    result += cell.GetScore();
                result += GetSpriteOverdraw() / 3000d;
                if (Cells.Count > 16)
                    result *= 2d;
                cachedScore = JavaRound(result * 100000d) / 100000d;
            }
            return cachedScore;
        }

        // Math.round(double): floor(x + 0.5) を long にしたもの
        static double JavaRound(double v) => (double)(long)Math.Floor(v + 0.5d);

        public bool RebuildFrom(List<SpriteCell> sprCells)
        {
            // sprCells が Cells 自身のこともあるので写してから作り直す
            var copy = new List<SpriteCell>(sprCells);
            Reset();
            foreach (var cell in copy)
                AddCell(cell);
            return IsComplete;
        }

        void RebuildWithout(SpriteCell cell)
        {
            var copy = new List<SpriteCell>(Cells);
            // List.remove(Object) と同じく、位置と大きさが同じ最初のものを除く
            int idx = copy.FindIndex(c => c.Equals(cell));
            if (idx >= 0)
                copy.RemoveAt(idx);
            RebuildFrom(copy);
        }

        static double GetBaseScore(List<SpriteCell> cells)
        {
            double result = 0d;
            foreach (var sc in cells)
                result += sc.GetBaseScore();
            return result;
        }

        List<SpriteCell> GetCoveredCells(SpriteCell cell) => Cells.Where(sc => cell.Contains(sc)).ToList();

        void OptimizeMergeForPart(SpriteCell cell)
        {
            double bestGain = 0d;
            SpriteCell bestGainCell = null;
            List<SpriteCell> bestCovered = null;

            int x = cell.X, y = cell.Y;
            int w = cell.Width / 8, h = cell.Height / 8;

            while (h < 4)
            {
                while (w < 4)
                {
                    var sc = new SpriteCell(x, y, w * 8, h * 8, cell.Opt);
                    var covered = GetCoveredCells(sc);
                    double coveredScore = GetBaseScore(covered);
                    if (coveredScore > 0d)
                    {
                        double gain = coveredScore - sc.GetBaseScore();
                        if (gain > bestGain)
                        {
                            bestGain = gain;
                            bestGainCell = sc;
                            bestCovered = covered;
                        }
                    }
                    w++;
                }
                w = 1;
                h++;
            }

            w = cell.Width / 8;
            h = cell.Height / 8;
            x = cell.X + (w * 8);
            y = cell.Y + (h * 8);

            while (h < 4)
            {
                while (w < 4)
                {
                    var sc = new SpriteCell(x - (w * 8), y - (h * 8), w * 8, h * 8, cell.Opt);
                    var covered = GetCoveredCells(sc);
                    double coveredScore = GetBaseScore(covered);
                    if (coveredScore > 0d)
                    {
                        double gain = coveredScore - sc.GetBaseScore();
                        if (gain > bestGain)
                        {
                            bestGain = gain;
                            bestGainCell = sc;
                            bestCovered = covered;
                        }
                    }
                    w++;
                }
                w = 1;
                h++;
            }

            if (bestGainCell != null)
            {
                Cells.Add(bestGainCell);
                // List.removeAll と同じく、位置と大きさが同じものをすべて除く (足したばかりのものも含む)
                Cells.RemoveAll(c => bestCovered.Any(b => b.Equals(c)));
            }
        }

        void OptimizeMerge()
        {
            StableSort(Cells, SpriteCell.SizeComparator);
            foreach (var cell in new List<SpriteCell>(Cells))
                OptimizeMergeForPart(cell);
        }

        void OptimizePos()
        {
            StableSort(Cells, SpriteCell.SizeComparator);
            var copy = new List<SpriteCell>(Cells);
            Reset();
            foreach (var cell in copy)
                AddCell(SpriteCell.OptimizePosition(Image, W, H, cell));
        }

        void OptimizeOverdraw()
        {
            StableSort(Cells, SpriteCell.SizeComparator);
            var copy = new List<SpriteCell>(Cells);
            Reset();
            foreach (var cell in copy)
            {
                cell.OptimizeOverdraw(Image, W, H, copy);
                AddCell(cell);
            }
        }

        void OptimizeSizeForPart(SpriteCell cell, bool fromOriginalImage)
        {
            RebuildWithout(cell);
            var newCell = SpriteCell.OptimizeSize(fromOriginalImage ? Image : CoverageImage, W, H, cell);
            if (newCell != null)
                AddCell(newCell);
        }

        void OptimizeSize(bool fromOriginalImage)
        {
            double score;
            double newScore = GetScore();
            double conv = 1d;

            do
            {
                score = newScore;
                StableSort(Cells, SpriteCell.SizeComparator);
                // 小さい区画から (cells は途中で作り直されるので、毎回その時点の i 番目を使う)
                for (int i = Cells.Count - 1; i >= 0; i--)
                    OptimizeSizeForPart(Cells[i], fromOriginalImage);

                newScore = GetScore();
                conv /= 2;
                conv += score - newScore;
            }
            while (newScore != score && Math.Abs(conv) > 0.0005d);
        }

        public void FixPos()
        {
            var copy = new List<SpriteCell>(Cells);
            Reset();
            foreach (var cell in copy)
                AddCell(SpriteCell.FixPosition(W, H, cell));
        }

        public void FastOptimize()
        {
            FixPos();

            double score;
            double minScore = GetScore();
            var best = new Solution(this);
            int conv = 0;

            while (true)
            {
                OptimizeMerge();
                OptimizePos();
                OptimizeSize(false);
                OptimizeOverdraw();
                FixPos();

                score = GetScore();
                if (score < minScore)
                {
                    minScore = score;
                    best.RebuildFrom(Cells);
                    conv = 0;
                }
                if (conv++ >= 3)
                    break;

                OptimizeMerge();
                OptimizePos();
                OptimizeSize(true);
                OptimizeOverdraw();
                FixPos();

                score = GetScore();
                if (score < minScore)
                {
                    minScore = score;
                    best.RebuildFrom(Cells);
                    conv = 0;
                }
                if (conv++ >= 3)
                    break;
            }

            RebuildFrom(best.Cells);
        }

        public int CompareTo(Solution o) => GetScore().CompareTo(o.GetScore());

        public override string ToString() => "Nb sprite=" + Cells.Count + " numTile=" + NumTile + " - Score=" + GetScore();
    }

    /// <summary>遺伝的アルゴリズム (Java 版はスレッドで並べて回すが、ここでは順に回す)。</summary>
    sealed class SolutionOptimizer
    {
        sealed class Branch
        {
            public readonly int Id;
            public readonly List<Solution> Solutions = new();
            public double BestScore = double.MaxValue;
            public long Gen;
            readonly SolutionOptimizer owner;

            public Branch(SolutionOptimizer owner, int id, Solution baseSolution)
            {
                this.owner = owner;
                Id = id;
                AddSolution(baseSolution);
            }

            public Solution GetRandomGoodSolution() => Solutions[JRandom.NextInt(Math.Min(Solutions.Count, 10))];
            public Solution GetRandomSolution() => Solutions[JRandom.NextInt(Solutions.Count)];

            public void AddSolution(Solution solution)
            {
                Gen++;
                // Collections.binarySearch と同じ位置に入れる
                int lo = 0, hi = Solutions.Count - 1, index = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >>> 1;
                    int c = Solutions[mid].CompareTo(solution);
                    if (c < 0) lo = mid + 1;
                    else if (c > 0) hi = mid - 1;
                    else { index = mid; break; }
                }
                Solutions.Insert(index >= 0 ? index : lo, solution);
                if (Solutions.Count > owner.solutionPoolSize)
                    Solutions.RemoveAt(Solutions.Count - 1);

                double score = solution.GetScore();
                if (score < BestScore)
                {
                    BestScore = score;
                    if (score < owner.globalBestScore)
                        owner.globalBestScore = score;
                }
            }

            public int CompareTo(Branch sb)
            {
                if (BestScore == sb.BestScore)
                    return Gen.CompareTo(sb.Gen);
                if (BestScore == owner.globalBestScore)
                    return -1;
                if (sb.BestScore == owner.globalBestScore)
                    return 1;
                if (Gen / 1000L == sb.Gen / 1000L)
                    return BestScore.CompareTo(sb.BestScore);
                return Gen.CompareTo(sb.Gen);
            }
        }

        const int MaxBranch = 1024;
        readonly int solutionPoolSize = 64;
        readonly SpriteCutter cutter;
        readonly long maxIteration;
        readonly List<Branch> branches = new();
        readonly Dictionary<int, Branch> branchMap = new();
        int curBranchId = 1;
        int curBranchTask;
        double globalBestScore = double.MaxValue;
        long iterations;

        public SolutionOptimizer(SpriteCutter cutter, List<Solution> bases, long numIteration)
        {
            this.cutter = cutter;
            maxIteration = numIteration;
            foreach (var b in bases)
                CreateNewBranch(b);
        }

        public long Run()
        {
            int maxItMul = 1;
            while (true)
            {
                RunTask();
                iterations++;

                if (maxIteration > 0 && iterations >= maxIteration * maxItMul)
                {
                    int numSprite = GetBestSolution().Cells.Count;
                    if (numSprite > 16 && numSprite < 19 && maxItMul < 10)
                        maxItMul++;
                    else
                        break;
                }
            }
            return iterations;
        }

        Branch CreateNewBranch(Solution b)
        {
            if (branches.Count >= MaxBranch)
            {
                SpriteCutter.StableSort(branches, (x, y) => x.CompareTo(y));
                var remove = branches[^1];
                branches.RemoveAt(branches.Count - 1);
                branchMap.Remove(remove.Id);
            }
            var result = new Branch(this, curBranchId, b);
            branches.Add(result);
            branchMap[curBranchId] = result;
            curBranchId++;
            return result;
        }

        public Solution GetBestSolution() => branches[0].Solutions[0];

        void AddSolution(int branchId, Solution solution)
        {
            if (!solution.IsComplete)
                return;
            if (branchMap.TryGetValue(branchId, out var branch))
                branch.AddSolution(solution);
            else
                CreateNewBranch(solution);
        }

        void RunTask()
        {
            int size = branches.Count;
            if ((JRandom.NextInt() & 0xF) != 0)
            {
                // 区画の変異
                var branch = branches[curBranchTask];
                var source = branch.GetRandomSolution();
                int numMutation = (JRandom.NextInt() & 3) + 1;
                curBranchTask = (curBranchTask + 1) % size;

                var cells = new List<SpriteCell>(source.Cells);
                int n = cells.Count;
                var result = new Solution(cutter);
                for (int i = 0; i < Math.Min(n, numMutation); i++)
                {
                    int k = JRandom.NextInt(cells.Count);
                    var part = cells[k];
                    cells.RemoveAt(k);
                    foreach (var p in part.Mutate())
                        result.AddCell(p);
                }
                foreach (var cell in cells)
                {
                    result.AddCell(cell);
                    if (result.IsComplete)
                        break;
                }
                AddSolution(branch.Id, result);
            }
            else
            {
                // 2 つの解を混ぜて新しい枝を作る
                Branch b1, b2;
                Solution s1, s2;
                if (JRandom.NextBoolean())
                {
                    b1 = branches[JRandom.NextInt(Math.Min(size, 50))];
                    b2 = branches[JRandom.NextInt(Math.Min(size, 50))];
                    s1 = b1.GetRandomGoodSolution();
                    s2 = b2.GetRandomGoodSolution();
                }
                else
                {
                    b1 = branches[JRandom.NextInt(size)];
                    b2 = branches[JRandom.NextInt(size)];
                    s1 = b1.GetRandomSolution();
                    s2 = b2.GetRandomSolution();
                }
                int id = curBranchId++;

                var cells1 = new List<SpriteCell>(s1.Cells);
                var cells2 = new List<SpriteCell>(s2.Cells);
                var result = new Solution(cutter);
                while (cells1.Count > 0 && cells2.Count > 0 && !result.IsComplete)
                {
                    SpriteCell cell;
                    if (JRandom.NextBoolean())
                    {
                        int k = JRandom.NextInt(cells1.Count);
                        cell = cells1[k];
                        cells1.RemoveAt(k);
                    }
                    else
                    {
                        int k = JRandom.NextInt(cells2.Count);
                        cell = cells2[k];
                        cells2.RemoveAt(k);
                    }
                    result.AddCell(cell);
                }
                AddSolution(id, result);
            }
        }
    }

    readonly byte[] image;
    readonly int w;
    readonly int h;
    readonly int numOpaquePixel;

    public SpriteCutter(byte[] image8bpp, int imgW, int imgH)
    {
        image = image8bpp;
        w = imgW;
        h = imgH;
        numOpaquePixel = ImageUtil.GetOpaquePixelCount(image, w, h, new JRect(w, h));
    }

    /// <summary>フレーム全体を 32x32 の VDP スプライトで覆う解。</summary>
    internal Solution GetDefaultSolution()
    {
        int cw = w / 8, ch = h / 8;
        int numCellW = (cw + 3) / 4;
        int numCellH = (ch + 3) / 4;
        int lastCellW = cw & 3;
        int lastCellH = ch & 3;
        if (lastCellW == 0) lastCellW = 4;
        if (lastCellH == 0) lastCellH = 4;

        var sprites = new List<JRect>();
        for (int xc = 0; xc < numCellW; xc++)
            for (int yc = 0; yc < numCellH; yc++)
                sprites.Add(new JRect(xc * 32, yc * 32, xc == numCellW - 1 ? lastCellW * 8 : 32, yc == numCellH - 1 ? lastCellH * 8 : 32));

        return new Solution(this, sprites, OptimizationType.NONE);
    }

    CellGrid BuildGrid(int cellSize, int offX, int offY, OptimizationType opt)
    {
        var bounds = new JRect(w, h);
        var grid = new CellGrid(offX, offY, (w + (cellSize * 2)) / cellSize, (h + (cellSize * 2)) / cellSize);
        for (int x = offX, xc = 0; x < w + cellSize; x += cellSize, xc++)
            for (int y = offY, yc = 0; y < h + cellSize; y += cellSize, yc++)
            {
                var tileRect = new SpriteCell(x, y, cellSize, cellSize, opt);
                if (!ImageUtil.IsTransparent(image, w, h, tileRect.Intersection(bounds)))
                    grid.Set(xc, yc, tileRect);
            }
        return grid;
    }

    public CellGrid GetBestGrid(int cellSize, OptimizationType opt)
    {
        int cellMask = cellSize - 1;
        int bestScore = int.MaxValue;
        CellGrid best = null;
        for (int offX = -cellMask; offX <= 0; offX++)
            for (int offY = -cellMask; offY <= 0; offY++)
            {
                var grid = BuildGrid(cellSize, offX, offY, opt);
                int used = grid.GetUsedCells(false);
                if (used < bestScore)
                {
                    bestScore = used;
                    best = grid;
                }
            }
        return best;
    }

    public List<CellGrid> GetBestGrids(int cellSize, OptimizationType opt)
    {
        int cellMask = cellSize - 1;
        int bestScore = int.MaxValue;
        var result = new List<CellGrid>();
        for (int offX = -cellMask; offX <= 0; offX++)
            for (int offY = -cellMask; offY <= 0; offY++)
            {
                var grid = BuildGrid(cellSize, offX, offY, opt);
                int used = grid.GetUsedCells(false);
                if (used <= bestScore * 1.2)
                {
                    bestScore = used;
                    result.Add(grid);
                }
            }
        return result;
    }

    internal Solution GetSolution(CellGrid grid, OptimizationType opt) =>
        new(this, grid.GetSpriteCells().Select(sc => (JRect)sc), opt);
}
