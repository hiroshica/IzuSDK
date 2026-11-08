using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sgdk.Rescomp.Imaging;
using Sgdk.Rescomp.Types;

namespace Sgdk.Rescomp.Resources;

// [EXT] SPRX: resources for the SPRJ resource type (JSON sprite -> extended frames, see inc/ext/sprite_eng_ext.h).

public enum SprxVramMode { RESIDENT = 0, TRANSFER = 1 }

/// <summary>Box kinds of the current .res (one per compile). Outputs the SprxBoxKind enum in the header.</summary>
public sealed class SprxKinds : Resource
{
    readonly List<string> fixedOrder;
    readonly SortedSet<string> used = new(StringComparer.Ordinal);
    public readonly string SourceFile;

    SprxKinds(List<string> fixedOrder, string sourceFile) : base("sprx_box_kinds")
    {
        this.fixedOrder = fixedOrder;
        SourceFile = sourceFile;
        Global = false;
    }

    public static SprxKinds Find() => (SprxKinds)Compiler.GetResources(typeof(SprxKinds)).FirstOrDefault();

    /// <summary>Kinds of the current .res (created and registered on first use).</summary>
    public static SprxKinds Get()
    {
        var k = Find();
        if (k == null)
        {
            k = new SprxKinds(null, null);
            Compiler.AddResource(k);
        }
        return k;
    }

    /// <summary>Create from a kinds file (SPRJ_KINDS), not registered (the processor returns it).</summary>
    public static SprxKinds FromFile(string file)
    {
        if (Find() != null)
            throw new ArgumentException("SPRJ_KINDS must be declared only once and before any SPRJ resource");

        var list = new List<string>();
        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("#"))
                continue;
            if (list.Contains(line))
                throw new ArgumentException("SPRJ_KINDS '" + file + "': duplicated kind '" + line + "'");
            list.Add(line);
        }
        if (list.Count > 255)
            throw new ArgumentException("SPRJ_KINDS '" + file + "': too many kinds (max = 255)");

        var k = new SprxKinds(list, file);
        CheckNames(list);
        return k;
    }

    public bool IsFixed => fixedOrder != null;

    public void Register(string kind, string where)
    {
        if (fixedOrder != null && !fixedOrder.Contains(kind))
            throw new ArgumentException(where + ": box kind '" + kind + "' is not declared in SPRJ_KINDS file '" + SourceFile + "'");
        used.Add(kind);
    }

    public IReadOnlyList<string> Kinds => fixedOrder ?? used.ToList();

    public int GetValue(string kind)
    {
        int v = fixedOrder != null ? fixedOrder.IndexOf(kind) : used.ToList().IndexOf(kind);
        if (v < 0)
            throw new ArgumentException("SPRJ: unknown box kind '" + kind + "'");
        return v;
    }

    public static string EnumName(string kind) =>
        "SPRX_KIND_" + Regex.Replace(kind, "[^A-Za-z0-9]", "_").ToUpperInvariant();

    static void CheckNames(IEnumerable<string> kinds)
    {
        var names = new Dictionary<string, string>();
        foreach (var k in kinds)
        {
            var n = EnumName(k);
            if (names.TryGetValue(n, out var other))
                throw new ArgumentException("SPRJ: box kinds '" + other + "' and '" + k + "' give the same enum name " + n);
            names[n] = k;
        }
    }

    public override int InternalHashCode() => 0x5B7C;
    public override bool InternalEquals(object obj) => ReferenceEquals(this, obj);
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => 0;
    public override int TotalSize() => 0;

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        var kinds = Kinds;
        CheckNames(kinds);

        outH.Append("#ifndef SPRX_BOX_KIND_DEFINED\n");
        outH.Append("#define SPRX_BOX_KIND_DEFINED\n");
        outH.Append("// SPRJ box kinds (" + (IsFixed ? "order of '" + JavaCompat.GetFileName(SourceFile, true) + "'" : "sorted by name") + ")\n");
        outH.Append("typedef enum\n{\n");
        for (int i = 0; i < kinds.Count; i++)
            outH.Append("    " + EnumName(kinds[i]) + " = " + i + ",    // \"" + kinds[i] + "\"\n");
        outH.Append("    SPRX_KIND_MAX = " + kinds.Count + "\n");
        outH.Append("} SprxBoxKind;\n");
        outH.Append("#endif // SPRX_BOX_KIND_DEFINED\n\n");
    }
}

/// <summary>SprxBoxList: boxes of a frame (stored in AnimationFrame.collision).</summary>
public sealed class SprxBoxListRes : Resource
{
    public sealed record Box(string Kind, int Index, int X, int Y, int W, int H, string Name);

    public readonly List<Box> Boxes;
    readonly int hc;

    public SprxBoxListRes(string id, List<Box> boxes) : base(id)
    {
        Boxes = boxes;
        int h = boxes.Count;
        foreach (var b in boxes)
            h = (h * 31) ^ b.Kind.GetHashCode() ^ (b.Index << 4) ^ (b.X << 8) ^ (b.Y << 12) ^ (b.W << 16) ^ (b.H << 24);
        hc = h;
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) =>
        obj is SprxBoxListRes o && o.Boxes.Count == Boxes.Count
        && Boxes.Zip(o.Boxes).All(p => p.First.Kind == p.Second.Kind && p.First.Index == p.Second.Index && p.First.X == p.Second.X
                                       && p.First.Y == p.Second.Y && p.First.W == p.Second.W && p.First.H == p.Second.H);
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => 2 + (Boxes.Count * 6);
    public override int TotalSize() => ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        var kinds = SprxKinds.Get();
        var sorted = Boxes.OrderBy(b => kinds.GetValue(b.Kind)).ThenBy(b => b.Index).ToList();

        Util.Decl(outS, outH, "SprxBoxList", Id, 2, Global);
        outS.Append("    dc.w    " + sorted.Count + "\n");
        foreach (var b in sorted)
            // no inline comment: SGDK assembles with -x assembler-with-cpp -Wa,--bitwise-or ('|' is not a comment there)
            outS.Append("    dc.b    " + kinds.GetValue(b.Kind) + ", " + b.Index + ", " + (b.X & 0xFF) + ", " + (b.Y & 0xFF) + ", " + b.W + ", " + b.H + "\n");
        outS.Append("\n");
    }
}

/// <summary>Extended AnimationFrame (standard frame + FrameVDPSpriteExt array).</summary>
public sealed class SprxFrameRes : Resource
{
    public sealed record VdpPart(int OffsetX, int OffsetY, int OffsetXFlip, int OffsetYFlip, int Wt, int Ht, int ExtAttr);

    public readonly int Timer;
    public readonly Tileset Tileset;
    public readonly SprxBoxListRes BoxList;
    public readonly List<VdpPart> Sprites;
    readonly int hc;

    public SprxFrameRes(string id, int timer, Tileset tileset, SprxBoxListRes boxList, List<VdpPart> sprites) : base(id)
    {
        Timer = timer;
        Tileset = tileset;
        BoxList = boxList;
        Sprites = sprites;

        foreach (var s in sprites)
            if (s.OffsetX < 0 || s.OffsetX > 255 || s.OffsetY < 0 || s.OffsetY > 255 || s.OffsetXFlip < 0 || s.OffsetXFlip > 255 || s.OffsetYFlip < 0 || s.OffsetYFlip > 255)
                throw new ArgumentException("SPRJ frame '" + id + "': sprite offset out of range (0..255), the sprite bounding box is too large");

        int h = timer ^ (tileset.GetHashCode() << 3) ^ (boxList?.GetHashCode() ?? 0);
        foreach (var s in sprites)
            h = (h * 31) ^ s.GetHashCode();
        hc = h;
    }

    public int NumSprite => Sprites.Count;
    public int NumTile => Tileset.NumTile;

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) =>
        obj is SprxFrameRes f && Timer == f.Timer && Tileset.Equals(f.Tileset) && Sprites.SequenceEqual(f.Sprites)
        && (ReferenceEquals(BoxList, f.BoxList) || (BoxList != null && BoxList.Equals(f.BoxList)));
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => 1 + 1 + 4 + 4 + (Sprites.Count * (6 + 2));
    public override int TotalSize() => ShallowSize() + Tileset.TotalSize() + (BoxList?.TotalSize() ?? 0);

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "AnimationFrame", Id, 2, Global);
        outS.Append("    dc.w    " + (((NumSprite << 8) & 0xFF00) | (Timer & 0xFF)) + "\n");
        outS.Append("    dc.l    " + Tileset.Id + "\n");
        outS.Append("    dc.l    " + (BoxList == null ? "0" : BoxList.Id) + "\n");
        // FrameVDPSprite[]: offsetY, offsetYFlip, size, offsetX, offsetXFlip, numTile
        foreach (var s in Sprites)
        {
            int size = ((s.Wt - 1) << 2) | (s.Ht - 1);
            outS.Append("    dc.w    " + ((s.OffsetY << 8) | (s.OffsetYFlip & 0xFF)) + "\n");
            outS.Append("    dc.w    " + ((size << 8) | (s.OffsetX & 0xFF)) + "\n");
            outS.Append("    dc.w    " + ((s.OffsetXFlip << 8) | ((s.Wt * s.Ht) & 0xFF)) + "\n");
        }
        // FrameVDPSpriteExt[]: tile offset / flip / palette
        foreach (var s in Sprites)
            outS.Append("    dc.w    " + s.ExtAttr + "\n");
        outS.Append("\n");
    }
}

/// <summary>Animation of extended frames (always loops to frame 0).</summary>
public sealed class SprxAnimationRes : Resource
{
    public readonly List<SprxFrameRes> Frames;
    readonly int hc;

    public SprxAnimationRes(string id, List<SprxFrameRes> frames) : base(id)
    {
        if (frames.Count > 255)
            throw new ArgumentException("SPRJ animation '" + id + "' has " + frames.Count + " frames (max = 255)");
        Frames = frames;
        hc = JavaCompat.ListHashCode(frames);
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) => obj is SprxAnimationRes a && Frames.SequenceEqual(a.Frames);
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => (Frames.Count * 4) + 1 + 1 + 4;
    public override int TotalSize() => Frames.Distinct().Sum(f => f.TotalSize()) + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, null, Id + "_frames", 2, false);
        foreach (var frame in Frames)
            outS.Append("    dc.l    " + frame.Id + "\n");
        outS.Append("\n");

        Util.Decl(outS, outH, "Animation", Id, 2, Global);
        outS.Append("    dc.w    " + (Frames.Count << 8) + "\n");
        outS.Append("    dc.l    " + Id + "_frames\n");
        outS.Append("\n");
    }
}

/// <summary>SPRJ main resource: SpriteDefinition (name_def) + SprxInfo (name).</summary>
public sealed class SprxSpriteRes : Resource
{
    public readonly SprxVramMode Mode;
    public readonly int W, H, PivotX, PivotY;
    public readonly int MaxNumTile, MaxNumSprite, VramTiles;
    public readonly Palette Palette;
    public readonly Tileset ResidentTiles;
    public readonly List<SprxAnimationRes> Animations = new();
    public readonly List<string> AnimationNames = new();
    readonly int hc;

    sealed class PartImage
    {
        public readonly int W, H;
        public readonly byte[] Pixels;

        public PartImage(int w, int h, byte[] pixels) { W = w; H = h; Pixels = pixels; }

        public PartImage Flip(bool hflip, bool vflip)
        {
            var r = new byte[Pixels.Length];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    r[(y * W) + x] = Pixels[((vflip ? H - 1 - y : y) * W) + (hflip ? W - 1 - x : x)];
            return new PartImage(W, H, r);
        }

        public bool SameAs(PartImage o) => o.W == W && o.H == H && o.Pixels.AsSpan().SequenceEqual(Pixels);
        public int NumTile => (W / 8) * (H / 8);
    }

    // a part reference: unique part index + flip to apply to the unique part to get the referenced part
    readonly record struct PartRef(int Unique, bool FlipH, bool FlipV);

    public SprxSpriteRes(string id, string jsonFile, SprJsonDocument doc, SprxVramMode mode, Compression compression, double timeDiv,
        Func<string, string> resolveImage) : base(id)
    {
        Mode = mode;
        string where = "SPRJ '" + id + "'";

        if (mode == SprxVramMode.TRANSFER && (compression == Compression.AUTO || compression == Compression.APLIB))
            Console.WriteLine("Warning: " + where + " uses TRANSFER mode with " + compression
                              + " compression: tiles are unpacked on each frame change, NONE or FAST is recommended");

        // ---- images ----
        var images = new Dictionary<string, (string File, byte[] Pixels, int W, int H, int Pal)>();
        (string File, byte[] Pixels, int W, int H, int Pal) GetImage(string name)
        {
            if (images.TryGetValue(name, out var img))
                return img;
            string file = resolveImage(name);
            var px = ImageUtil.GetImageAs8bpp(file, false, true)
                     ?? throw new ArgumentException(where + ": RGB image '" + file + "' does not contains palette data");
            int w = ImageUtil.GetBasicInfo(file).W;
            int h = px.Length / w;
            int pal;
            try
            {
                pal = ImageUtil.GetSpritePaletteIndex(px, w, h);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(where + ": '" + file + "' uses more than 1 palette (16 colors)", e);
            }
            Compiler.AddResourceFile(file);
            img = (file, px, w, h, pal);
            images[name] = img;
            return img;
        }

        // ---- frames / parts actually used ----
        var usedFrames = doc.Animations.SelectMany(a => a.Entries.Select(e => e.Frame)).Distinct().ToList();
        var usedPartIds = usedFrames.SelectMany(f => doc.GetFrame(f).Sprites.Select(s => s.Part)).Distinct().OrderBy(p => p).ToList();

        // unique part images (identical or flipped identical parts are merged)
        var uniques = new List<PartImage>();
        var partRefs = new Dictionary<int, PartRef>();
        foreach (int pid in usedPartIds)
        {
            var part = doc.GetPart(pid);
            var img = GetImage(part.Source.Image);
            var src = part.Source;
            if (src.X < 0 || src.Y < 0 || src.X + part.Width > img.W || src.Y + part.Height > img.H)
                throw new ArgumentException(where + ": part " + pid + " source rectangle is outside of image '" + img.File + "'");

            var px = new byte[part.Width * part.Height];
            for (int y = 0; y < part.Height; y++)
                for (int x = 0; x < part.Width; x++)
                    px[(y * part.Width) + x] = (byte)(img.Pixels[((src.Y + y) * img.W) + src.X + x] & 0x0F);
            var pi = new PartImage(part.Width, part.Height, px);

            PartRef? found = null;
            for (int u = 0; u < uniques.Count && found == null; u++)
            {
                var un = uniques[u];
                if (un.W != pi.W || un.H != pi.H) continue;
                if (un.SameAs(pi)) found = new PartRef(u, false, false);
                else if (un.Flip(true, false).SameAs(pi)) found = new PartRef(u, true, false);
                else if (un.Flip(false, true).SameAs(pi)) found = new PartRef(u, false, true);
                else if (un.Flip(true, true).SameAs(pi)) found = new PartRef(u, true, true);
            }
            if (found == null)
            {
                uniques.Add(pi);
                found = new PartRef(uniques.Count - 1, false, false);
            }
            partRefs[pid] = found.Value;
        }

        // palette (colors managed like a PALETTE resource): name_palette from the first image
        var firstImage = usedPartIds.Count > 0 ? GetImage(doc.GetPart(usedPartIds[0]).Source.Image) : default;
        if (firstImage.File == null)
            throw new ArgumentException(where + ": no sprite part used by animations");
        foreach (var img in images.Values)
            if (img.File != firstImage.File && img.Pal != firstImage.Pal)
                Console.WriteLine("Warning: " + where + ": image '" + img.File + "' uses palette block " + img.Pal + " but '" + firstImage.File
                                  + "' uses " + firstImage.Pal + ", only " + id + "_palette (from the first image) is generated");
        string palId = id + "_palette";
        if (Compiler.GetResourceById(palId) != null)
            throw new ArgumentException(where + ": resource '" + palId + "' already exists (SPRJ generates it, remove the PALETTE line)");
        Palette = new Palette(palId, firstImage.File, firstImage.Pal * 16, 16, true);
        Compiler.AddResource(Palette);

        // ---- bounding box (symmetric around pivot so whole sprite flip keeps the pivot) ----
        int halfW = 0, halfH = 0;
        foreach (int f in usedFrames)
            foreach (var s in doc.GetFrame(f).Sprites)
            {
                var p = doc.GetPart(s.Part);
                halfW = Math.Max(halfW, Math.Max(-s.X, s.X + p.Width));
                halfH = Math.Max(halfH, Math.Max(-s.Y, s.Y + p.Height));
            }
        if (halfW == 0) halfW = 4;
        if (halfH == 0) halfH = 4;
        W = halfW * 2;
        H = halfH * 2;
        PivotX = halfW;
        PivotY = halfH;

        // ---- tile layout ----
        Tileset EmptyTileset()
        {
            var t = (Tileset)AddInternalResource(new Tileset(id + "_empty_tileset", false));
            t.Bin.Global = false;
            return t;
        }

        Tileset BuildTileset(string tsId, List<int> uniqueList)
        {
            if (uniqueList.Count == 0)
                return EmptyTileset();
            int cw = uniqueList.Max(u => uniques[u].W);
            int ch = uniqueList.Sum(u => uniques[u].H);
            var comp = new byte[cw * ch];
            var rects = new List<(int, int, int, int)>();
            int yy = 0;
            foreach (int u in uniqueList)
            {
                var un = uniques[u];
                for (int y = 0; y < un.H; y++)
                    Array.Copy(un.Pixels, y * un.W, comp, ((yy + y) * cw), un.W);
                rects.Add((0, yy, un.W, un.H));
                yy += un.H;
            }
            return (Tileset)AddInternalResource(new Tileset(tsId, comp, cw, ch, rects, compression, false));
        }

        // RESIDENT: all unique parts, global offsets
        var residentOffsets = new Dictionary<int, int>();
        if (mode == SprxVramMode.RESIDENT)
        {
            int off = 0;
            var all = Enumerable.Range(0, uniques.Count).ToList();
            foreach (int u in all)
            {
                residentOffsets[u] = off;
                off += uniques[u].NumTile;
            }
            if (off > 0x7FF)
                throw new ArgumentException(where + ": " + off + " resident tiles (max = 2047)");
            ResidentTiles = BuildTileset(id + "_resident_tileset", all);
            VramTiles = ResidentTiles.NumTile;
            if (VramTiles != off)
                throw new InvalidOperationException(where + ": internal error, resident tile count mismatch");
        }

        // ---- box kinds ----
        var kinds = SprxKinds.Get();

        // ---- frames & animations ----
        var frameCache = new Dictionary<(int Frame, int Timer), SprxFrameRes>();
        SprxFrameRes BuildFrame(int frameIndex, int timer)
        {
            if (frameCache.TryGetValue((frameIndex, timer), out var cached))
                return cached;

            var jf = doc.GetFrame(frameIndex);
            string fid = id + "_frame" + frameIndex + (timer != 0 ? "_t" + timer : "");

            // tiles
            Dictionary<int, int> offsets;
            Tileset ts;
            if (mode == SprxVramMode.RESIDENT)
            {
                offsets = residentOffsets;
                ts = EmptyTileset();
            }
            else
            {
                var local = new List<int>();
                foreach (var s in jf.Sprites)
                    if (!local.Contains(partRefs[s.Part].Unique))
                        local.Add(partRefs[s.Part].Unique);
                offsets = new Dictionary<int, int>();
                int off = 0;
                foreach (int u in local)
                {
                    offsets[u] = off;
                    off += uniques[u].NumTile;
                }
                ts = BuildTileset(fid + "_tileset", local);
            }

            // VDP sprites
            var parts = new List<SprxFrameRes.VdpPart>();
            foreach (var s in jf.Sprites)
            {
                var p = doc.GetPart(s.Part);
                var r = partRefs[s.Part];
                if (s.Palette < 0 || s.Palette > 3)
                    throw new ArgumentException(where + ": frame " + frameIndex + " sprite palette " + s.Palette + " out of range (0..3)");
                bool hf = s.FlipX ^ r.FlipH;
                bool vf = s.FlipY ^ r.FlipV;
                int tileOff = offsets[r.Unique];
                int ext = (s.Palette << 13) | (vf ? 0x1000 : 0) | (hf ? 0x0800 : 0) | tileOff;
                int ox = s.X + halfW;
                int oy = s.Y + halfH;
                parts.Add(new SprxFrameRes.VdpPart(ox, oy, W - (ox + p.Width), H - (oy + p.Height), p.Width / 8, p.Height / 8, ext));
            }

            // boxes
            SprxBoxListRes boxList = null;
            if (jf.Boxes.Count > 0)
            {
                var counters = new Dictionary<string, int>();
                var boxes = new List<SprxBoxListRes.Box>();
                foreach (var b in jf.Boxes)
                {
                    kinds.Register(b.Kind, where + " frame " + frameIndex);
                    counters.TryGetValue(b.Kind, out int idx);
                    counters[b.Kind] = idx + 1;
                    if (idx > 255)
                        throw new ArgumentException(where + ": frame " + frameIndex + " has too many '" + b.Kind + "' boxes");
                    boxes.Add(new SprxBoxListRes.Box(b.Kind, idx, b.X, b.Y, b.Width, b.Height, b.Name));
                }
                boxList = (SprxBoxListRes)AddInternalResource(new SprxBoxListRes(fid + "_boxes", boxes));
            }

            var fr = (SprxFrameRes)AddInternalResource(new SprxFrameRes(fid, timer, ts, boxList, parts));
            frameCache[(frameIndex, timer)] = fr;
            return fr;
        }

        int ToTimer(SprJsonAnimation a, SprJsonAnimEntry e)
        {
            double ms = e.DurationMs ?? doc.GetFrame(e.Frame).DefaultDurationMs;
            if (timeDiv == 0)
                return 0;
            int t = (int)Math.Round(ms / timeDiv, MidpointRounding.AwayFromZero);
            if (t <= 0)
            {
                Console.WriteLine("Warning: " + where + ": animation " + a.Index + " frame " + e.Frame + " duration " + ms + "ms / " + timeDiv
                                  + " gives " + t + " frame, using 1");
                t = 1;
            }
            if (t > 255)
                throw new ArgumentException(where + ": animation " + a.Index + " frame " + e.Frame + " duration " + ms + "ms / "
                                            + timeDiv.ToString(CultureInfo.InvariantCulture) + " = " + t + " frames (max = 255), use a larger timediv");
            return t;
        }

        int maxTile = 0, maxSprite = 0;
        foreach (var a in doc.Animations.OrderBy(a => a.Index))
        {
            var frames = a.Entries.Select(e => BuildFrame(e.Frame, ToTimer(a, e))).ToList();
            var anim = (SprxAnimationRes)AddInternalResource(new SprxAnimationRes(id + "_animation" + a.Index, frames));
            Animations.Add(anim);
            AnimationNames.Add(a.Name);
            maxTile = Math.Max(maxTile, frames.Max(f => f.NumTile));
            maxSprite = Math.Max(maxSprite, frames.Max(f => f.NumSprite));
        }

        MaxNumSprite = maxSprite;
        if (mode == SprxVramMode.TRANSFER)
        {
            MaxNumTile = maxTile;
            VramTiles = maxTile;
            if (maxTile > 0x7FF)
                throw new ArgumentException(where + ": " + maxTile + " tiles in a frame (max = 2047)");
        }
        else
            MaxNumTile = 0;

        Console.WriteLine(where + ": " + mode + " " + W + "x" + H + " pivot=(" + PivotX + "," + PivotY + ") parts=" + usedPartIds.Count
                          + " unique=" + uniques.Count + " vramTiles=" + VramTiles + " maxNumSprite=" + MaxNumSprite);

        hc = id.GetHashCode() ^ JavaCompat.ListHashCode(Animations);
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) => ReferenceEquals(this, obj);
    public override List<Bin> GetInternalBinResources() => new();
    public override int ShallowSize() => (Animations.Count * 4) + 2 + 2 + 4 + 2 + 4 + 2 + 2 + (4 + 2 + 2 + 4 + 2 + 2);
    public override int TotalSize() => Animations.Sum(a => a.TotalSize()) + Palette.TotalSize() + (ResidentTiles?.TotalSize() ?? 0) + ShallowSize();

    static string MacroName(string s) => Regex.Replace(s ?? "", "[^A-Za-z0-9]", "_").ToUpperInvariant();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, null, Id + "_animations", 2, false);
        foreach (var animation in Animations)
            outS.Append("    dc.l    " + animation.Id + "\n");
        outS.Append("\n");

        Util.Decl(outS, outH, "SpriteDefinition", Id + "_def", 2, true);
        outS.Append("    dc.w    " + W + "\n");
        outS.Append("    dc.w    " + H + "\n");
        outS.Append("    dc.l    " + Palette.Id + "\n");
        outS.Append("    dc.w    " + Animations.Count + "\n");
        outS.Append("    dc.l    " + Id + "_animations\n");
        outS.Append("    dc.w    " + MaxNumTile + "\n");
        outS.Append("    dc.w    " + MaxNumSprite + "\n");
        outS.Append("\n");

        Util.Decl(outS, outH, "SprxInfo", Id, 2, true);
        outS.Append("    dc.l    " + Id + "_def\n");
        outS.Append("    dc.w    " + (int)Mode + "\n");
        outS.Append("    dc.w    " + VramTiles + "\n");
        outS.Append("    dc.l    " + (ResidentTiles == null ? "0" : ResidentTiles.Id) + "\n");
        outS.Append("    dc.w    " + PivotX + "\n");
        outS.Append("    dc.w    " + PivotY + "\n");
        outS.Append("\n");

        outH.Append("#define " + Id + "_VRAM_TILES " + VramTiles + "    // " + Mode + "\n");
        var seen = new HashSet<string>();
        for (int i = 0; i < AnimationNames.Count; i++)
        {
            var n = Id + "_ANIM_" + MacroName(AnimationNames[i]);
            if (string.IsNullOrEmpty(AnimationNames[i]) || !seen.Add(n))
                continue;
            outH.Append("#define " + n + " " + i + "\n");
        }
    }
}
