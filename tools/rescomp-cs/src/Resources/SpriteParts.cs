using System.Text;
using Sgdk.Rescomp.Imaging;
using Sgdk.Rescomp.Tools;
using Sgdk.Rescomp.Types;

namespace Sgdk.Rescomp.Resources;

/// <summary>フレームの中の 1 つの VDP スプライト (Java 版の resource.internal.VDPSprite)。</summary>
public sealed class VDPSprite : Resource
{
    public readonly int OffsetX, OffsetY, Wt, Ht, OffsetXFlip, OffsetYFlip;
    readonly int hc;

    public VDPSprite(string id, int offX, int offY, int w, int h, int wf, int hf) : base(id)
    {
        OffsetX = offX;
        OffsetY = offY;
        Wt = w;
        Ht = h;
        OffsetXFlip = (wf * 8) - (offX + (w * 8));
        OffsetYFlip = (hf * 8) - (offY + (h * 8));

        if (OffsetX < 0 || OffsetX > 255 || OffsetY < 0 || OffsetY > 255)
            throw new ArgumentException("Error: sprite '" + id + "' offset X / Y is out of range (< 0 or > 255)");
        if (OffsetXFlip < 0 || OffsetXFlip > 255 || OffsetYFlip < 0 || OffsetYFlip > 255)
            throw new ArgumentException("Error: sprite '" + id + "' flipped offset X / Y is out of range (< 0 or > 255)");

        hc = OffsetX ^ OffsetXFlip ^ (OffsetY << 8) ^ (OffsetYFlip << 8) ^ (Wt << 16) ^ (Ht << 24);
    }

    public VDPSprite(string id, SpriteCell sprite, int wf, int hf) : this(id, sprite.X, sprite.Y, sprite.Width / 8, sprite.Height / 8, wf, hf) { }

    public int FormattedSize => ((Wt - 1) << 2) | (Ht - 1);

    internal void InternalOutS(StringBuilder outS)
    {
        // 並び: offsetY, offsetYFlip, size, offsetX, offsetXFlip, numTile
        outS.Append("    dc.w    " + ((OffsetY << 8) | (OffsetYFlip & 0xFF)) + "\n");
        outS.Append("    dc.w    " + ((FormattedSize << 8) | (OffsetX & 0xFF)) + "\n");
        outS.Append("    dc.w    " + ((OffsetXFlip << 8) | ((Ht * Wt) & 0xFF)) + "\n");
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) =>
        obj is VDPSprite v && OffsetX == v.OffsetX && OffsetY == v.OffsetY && Wt == v.Wt && Ht == v.Ht && OffsetXFlip == v.OffsetXFlip && OffsetYFlip == v.OffsetYFlip;
    public override List<Bin> GetInternalBinResources() => new();
    public override string ToString() => Id + ": [" + OffsetX + "," + OffsetY + "-" + (Wt * 8) + "," + (Ht * 8) + "]";
    public override int ShallowSize() => 6;
    public override int TotalSize() => ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        Util.Decl(outS, outH, "FrameVDPSprite", Id, 2, Global);
        InternalOutS(outS);
        outB.Write(OffsetY);
        outB.Write(OffsetYFlip);
        outB.Write(FormattedSize);
        outB.Write(OffsetX);
        outB.Write(OffsetXFlip);
        outB.Write(Ht * Wt);
        outS.Append("\n");
    }
}

public abstract record CollisionShape;
public sealed record CollisionBox(int X, int Y, int W, int H) : CollisionShape;
public sealed record CollisionCircle(int X, int Y, int Ray) : CollisionShape;

/// <summary>衝突判定 (Java 版の resource.internal.Collision)。</summary>
public sealed class Collision : Resource
{
    // Java 版の Box / Circle は hashCode を持たない (オブジェクトごとに違う値) ので、衝突判定はほかと 1 つにまとまらない。
    // それをまねるため、作るたびに違う番号を使う
    static int nextShapeHash = 0x2F6B1E5;

    public readonly CollisionType TypeHit;
    public readonly CollisionType TypeAttack = CollisionType.NONE;
    public readonly CollisionShape Hit;
    readonly int hc;

    public Collision(string id, CollisionShape collision) : base(id)
    {
        TypeHit = collision switch
        {
            CollisionBox => CollisionType.BOX,
            CollisionCircle => CollisionType.CIRCLE,
            _ => CollisionType.NONE,
        };
        Hit = collision;
        int shapeHash = unchecked(nextShapeHash += 0x61C88647);
        hc = (int)TypeHit ^ (TypeHit != CollisionType.NONE ? shapeHash : 0);
    }

    public override int InternalHashCode() => hc;
    // Java 版と同じく種類だけを比べる
    public override bool InternalEquals(object obj) => obj is Collision c && TypeHit == c.TypeHit && TypeAttack == c.TypeAttack;
    public override List<Bin> GetInternalBinResources() => new();

    void OutCollision(CollisionShape c, BinaryStream outB, StringBuilder outS, bool forceExport)
    {
        switch (c)
        {
            case CollisionBox b:
                if (b.X < -128 || b.X > 127 || b.Y < -128 || b.Y > 127)
                    Console.Error.WriteLine("Error: collision '" + Id + "' position X/Y is out of range (< -128 or > 127) !");
                if (b.W > 255 || b.H > 255)
                    Console.Error.WriteLine("Error: collision '" + Id + "' size W/H is out of range (> 255) !");
                outS.Append("    dc.w    " + ((b.X << 8) | (b.Y & 0xFF)) + "\n");
                outS.Append("    dc.w    " + ((b.W << 8) | (b.H & 0xFF)) + "\n");
                outB.Write(b.X); outB.Write(b.Y); outB.Write(b.W); outB.Write(b.H);
                break;
            case CollisionCircle ci:
                if (ci.X < -128 || ci.X > 127 || ci.Y < -128 || ci.Y > 127)
                    Console.Error.WriteLine("Error: collision '" + Id + "' position X/Y is out of range (< -128 or > 127) !");
                outS.Append("    dc.w    " + ((ci.X << 8) | (ci.Y & 0xFF)) + "\n");
                outS.Append("    dc.w    " + ci.Ray + "\n");
                outB.Write(ci.X); outB.Write(ci.Y);
                // Util.outB(short): 下位のバイトが先
                outB.Write(ci.Ray); outB.Write(ci.Ray >> 8);
                break;
            default:
                if (forceExport)
                {
                    outS.Append("    dc.w    0\n");
                    outS.Append("    dc.w    0\n");
                    outB.Write(0); outB.Write(0); outB.Write(0); outB.Write(0);
                }
                break;
        }
    }

    public override int ShallowSize() => 2 + 1 + 1 + (TypeAttack != CollisionType.NONE ? 8 : (Hit != null ? 4 : 0));
    public override int TotalSize() => ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        Util.Decl(outS, outH, "Collision", Id, 2, Global);
        outS.Append("    dc.w    " + (((int)TypeHit << 8) | ((int)TypeAttack & 0xFF)) + "\n");
        outB.Write((int)TypeHit);
        outB.Write((int)TypeAttack);
        OutCollision(Hit, outB, outS, TypeAttack != CollisionType.NONE);
        OutCollision(null, outB, outS, false);
        outS.Append("\n");
    }
}

/// <summary>アニメーションの 1 フレーム (Java 版の resource.internal.SpriteFrame)。</summary>
public sealed class SpriteFrame : Resource
{
    public readonly List<VDPSprite> VdpSprites = new();
    public readonly Collision Collision;
    public readonly Tileset Tileset;
    public readonly int Timer;
    readonly int hc;

    // マスクが同じフレームを探すときに使う
    internal readonly byte[] FrameImage;
    internal readonly int FrameW, FrameH;

    public SpriteFrame(string id, byte[] frameImage8bpp, int wf, int hf, int timer, CollisionType collisionType, Compression compression, List<SpriteCell> sprites)
        : base(id)
    {
        Timer = timer;
        FrameImage = frameImage8bpp;
        FrameW = wf * 8;
        FrameH = hf * 8;

        if (sprites.Count == 0)
        {
            Console.WriteLine("Sprite frame '" + id + "' is empty");
            Tileset = (Tileset)AddInternalResource(new Tileset(id + "_tileset", false));
        }
        else
        {
            int optNumTile = sprites.Sum(s => s.NumTile);
            Console.WriteLine("Sprite frame '" + id + "' - " + sprites.Count + " VDP sprites and " + optNumTile + " tiles");
            Tileset = (Tileset)AddInternalResource(new Tileset(id + "_tileset", FrameImage, wf * 8, hf * 8,
                sprites.Select(s => (s.X, s.Y, s.Width, s.Height)), compression, false));
        }

        if (collisionType != CollisionType.NONE)
        {
            CollisionShape c = collisionType switch
            {
                // フレームの 75% の大きさ
                CollisionType.BOX => new CollisionBox((wf * 8) * 1 / 4, (hf * 8) * 1 / 4, (wf * 8) * 3 / 4, (hf * 8) * 3 / 4),
                CollisionType.CIRCLE => new CollisionCircle((wf * 8) / 2, (hf * 8) / 2, (wf * 8) * 3 / 8),
                _ => null,
            };
            Collision = (Collision)AddInternalResource(new Collision(id + "_collision", c));
        }

        int ind = 0;
        foreach (var sprite in sprites)
            VdpSprites.Add(new VDPSprite(id + "_sprite" + ind++, sprite, wf, hf));

        hc = (timer << 16) ^ (Tileset?.GetHashCode() ?? 0) ^ JavaCompat.ListHashCode(VdpSprites) ^ (Collision?.GetHashCode() ?? 0);
    }

    public SpriteFrame(string id, byte[] frameImage8bpp, int wf, int hf, int timer, CollisionType collisionType, Compression compression,
        OptimizationType optType, OptimizationLevel optLevel)
        : this(id, frameImage8bpp, wf, hf, timer, collisionType, compression, ComputeSpriteCutting(id, frameImage8bpp, wf, hf, optType, optLevel)) { }

    static List<SpriteCell> ComputeSpriteCutting(string id, byte[] img, int wf, int hf, OptimizationType optType, OptimizationLevel optLevel)
    {
        List<SpriteCell> sprites;
        int fw = wf * 8, fh = hf * 8;

        if (optType == OptimizationType.NONE)
            sprites = SpriteCutter.GetFastOptimizedSpriteList(img, fw, fh, OptimizationType.NONE, false);
        else if (optLevel == OptimizationLevel.SLOW || optLevel == OptimizationLevel.MAX)
        {
            long iteration = optLevel == OptimizationLevel.SLOW ? 500000 : 5000000;
            sprites = SpriteCutter.GetSlowOptimizedSpriteList(img, fw, fh, iteration, optType);
            if (sprites.Count > 16 && optType != OptimizationType.MIN_SPRITE)
                sprites = SpriteCutter.GetSlowOptimizedSpriteList(img, fw, fh, iteration, OptimizationType.MIN_SPRITE);
        }
        else
        {
            bool optBetter = optLevel == OptimizationLevel.MEDIUM;
            sprites = SpriteCutter.GetFastOptimizedSpriteList(img, fw, fh, optType, optBetter);
            if (sprites.Count > 16 && optType != OptimizationType.MIN_SPRITE)
                sprites = SpriteCutter.GetFastOptimizedSpriteList(img, fw, fh, OptimizationType.MIN_SPRITE, optBetter);
            if (sprites.Count > 16 && !optBetter)
                sprites = SpriteCutter.GetFastOptimizedSpriteList(img, fw, fh, OptimizationType.MIN_SPRITE, true);
            if (sprites.Count > 16)
                sprites = SpriteCutter.GetSlowOptimizedSpriteList(img, fw, fh, 100000, OptimizationType.MIN_SPRITE);
        }

        if (sprites.Count > 16)
            throw new NotSupportedException("Sprite frame '" + id + "' uses " + sprites.Count
                + " internal sprites, that is above the limit (16), try to reduce the sprite size or split it.");

        // NONE のときは、空のフレームならスプライトを使わない
        if (sprites.Count > 0 && optType == OptimizationType.NONE && img.All(b => (b & 0xF) == 0))
            sprites.Clear();

        return sprites;
    }

    /// <summary>このフレームの分け方 (ほかのフレームで使い回す)。</summary>
    public List<SpriteCell> GetSprites() =>
        VdpSprites.Select(s => new SpriteCell(s.OffsetX, s.OffsetY, s.Wt * 8, s.Ht * 8, OptimizationType.BALANCED)).ToList();

    public int NumSprite => IsEmpty ? 0 : VdpSprites.Count;
    public bool IsEmpty => Tileset.IsEmpty;

    public bool IsOptimisable
    {
        get
        {
            if (VdpSprites.Count != 1)
                return false;
            var s = VdpSprites[0];
            return s.Wt * 8 == FrameW && s.Ht * 8 == FrameH && s.OffsetX == 0 && s.OffsetY == 0;
        }
    }

    public int NumTile => IsEmpty ? 0 : Tileset.NumTile;

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) =>
        obj is SpriteFrame f && Timer == f.Timer && Tileset.Equals(f.Tileset) && VdpSprites.SequenceEqual(f.VdpSprites)
        && (ReferenceEquals(Collision, f.Collision) || (Collision != null && Collision.Equals(f.Collision)));
    public override List<Bin> GetInternalBinResources() => new();
    public override string ToString() => Id + ": numTile=" + NumTile + " numSprite=" + NumSprite;
    public override int ShallowSize() => (VdpSprites.Count * 6) + 1 + 1 + 4 + 4;
    public override int TotalSize() => IsEmpty ? ShallowSize() : Tileset.TotalSize() + (Collision?.TotalSize() ?? 0) + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, "AnimationFrame", Id, 2, Global);
        int numSprite = IsOptimisable ? 0x81 : NumSprite;
        outS.Append("    dc.w    " + (((numSprite << 8) & 0xFF00) | (Timer & 0xFF)) + "\n");
        outS.Append("    dc.l    " + Tileset.Id + "\n");
        outS.Append("    dc.l    " + (Collision == null ? "0" : Collision.Id) + "\n");
        foreach (var sprite in VdpSprites)
            sprite.InternalOutS(outS);
        outS.Append("\n");
    }
}

/// <summary>アニメーション (Java 版の resource.internal.SpriteAnimation)。</summary>
public sealed class SpriteAnimation : Resource
{
    public readonly List<SpriteFrame> Frames = new();
    public int LoopIndex;
    readonly int hc;

    /// <param name="w">画像の幅 (タイル)</param>
    /// <param name="h">画像の高さ (タイル)</param>
    /// <param name="wf">フレームの幅 (タイル)</param>
    /// <param name="hf">フレームの高さ (タイル)</param>
    public SpriteAnimation(string id, byte[] image8bpp, int w, int h, int animIndex, int wf, int hf, int[] time, CollisionType collision,
        Compression compression, OptimizationType optType, OptimizationLevel optLevel, bool optDuplicate) : base(id)
    {
        int imgW = w * 8, imgH = h * 8;
        int maxFrame = w / wf;

        // 最後の透明でないフレームを探す
        int f = maxFrame - 1;
        while (f >= 0)
        {
            if (!ImageUtil.IsTransparent(image8bpp, imgW, imgH, new JRect(f * wf * 8, animIndex * hf * 8, wf * 8, hf * 8)))
                break;
            f--;
        }
        int numFrame = f + 1;

        byte[] FrameImage(int i) => ImageUtil.GetSubImage(image8bpp, imgW, i * wf * 8, animIndex * hf * 8, wf * 8, hf * 8);

        for (int i = 0; i < numFrame; i++)
        {
            var frameImage = FrameImage(i);

            int duplicate = 0;
            if (optDuplicate)
            {
                // 続く同じフレームを数える
                for (int j = i + 1; j < numFrame; j++)
                {
                    if (!frameImage.AsSpan().SequenceEqual(FrameImage(j)))
                        break;
                    duplicate++;
                }
            }

            int timer = time[Math.Min(time.Length - 1, i)] * (duplicate + 1);
            // マスクが同じフレームがすでにあれば、その分け方を使う
            var match = FindMatchingSpriteFrameMask(frameImage, wf * 8, hf * 8);
            var frame = match != null
                ? new SpriteFrame(id + "_frame" + i, frameImage, wf, hf, timer, collision, compression, match.GetSprites())
                : new SpriteFrame(id + "_frame" + i, frameImage, wf, hf, timer, collision, compression, optType, optLevel);
            frame = (SpriteFrame)AddInternalResource(frame);
            i += duplicate;

            Frames.Add(frame);
        }

        if (Frames.Count > 255)
            throw new ArgumentException("Sprite animation '" + id + "' has " + Frames.Count + " frames (max = 255)");

        hc = LoopIndex ^ JavaCompat.ListHashCode(Frames);
    }

    static SpriteFrame FindMatchingSpriteFrameMask(byte[] frameImage, int fw, int fh)
    {
        foreach (var res in Compiler.GetResources(typeof(SpriteFrame)))
        {
            var sf = (SpriteFrame)res;
            if (sf.FrameW != fw || sf.FrameH != fh || sf.FrameImage.Length != frameImage.Length)
                continue;
            bool same = true;
            for (int i = 0; i < frameImage.Length && same; i++)
                same = (sf.FrameImage[i] != 0) == (frameImage[i] != 0);
            if (same)
                return sf;
        }
        return null;
    }

    public bool IsEmpty => Frames.Count == 0;
    public int NumFrame => Frames.Count;
    public int MaxNumTile => Frames.Count == 0 ? 0 : Math.Max(0, Frames.Max(f => f.NumTile));
    public int MaxNumSprite => Frames.Count == 0 ? 0 : Math.Max(0, Frames.Max(f => f.NumSprite));

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) => obj is SpriteAnimation a && LoopIndex == a.LoopIndex && Frames.SequenceEqual(a.Frames);
    public override List<Bin> GetInternalBinResources() => new();
    public override string ToString() => Id + ": numFrame=" + Frames.Count + " maxNumTile=" + MaxNumTile + " maxNumSprite=" + MaxNumSprite;
    public override int ShallowSize() => (Frames.Count * 4) + 1 + 1 + 4;

    // Java 版は HashSet (同じフレームは 1 つ) で足す
    public override int TotalSize() => Frames.Distinct().Sum(f => f.TotalSize()) + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, null, Id + "_frames", 2, false);
        foreach (var frame in Frames)
            outS.Append("    dc.l    " + frame.Id + "\n");
        outS.Append("\n");

        Util.Decl(outS, outH, "Animation", Id, 2, Global);
        outS.Append("    dc.w    " + ((Frames.Count << 8) | (LoopIndex & 0xFF)) + "\n");
        outS.Append("    dc.l    " + Id + "_frames\n");
        outS.Append("\n");
    }
}

/// <summary>スプライト (Java 版の resource.Sprite)。</summary>
public sealed class Sprite : Resource
{
    public readonly int Wf, Hf;
    public readonly List<SpriteAnimation> Animations = new();
    public int MaxNumTile, MaxNumSprite;
    public readonly Palette Palette;
    readonly int hc;

    public Sprite(string id, string imgFile, int wf, int hf, Compression compression, int[][] time, CollisionType collision, OptimizationType optType,
        OptimizationLevel optLevel, bool showCut, bool optDuplicate) : base(id)
    {
        if (wf >= 32 || hf >= 32)
            throw new ArgumentException("SPRITE '" + id + "' has frame width or frame height >= 32 tiles (not supported)");

        Wf = wf;
        Hf = hf;

        var image = ImageUtil.GetImageAs8bpp(imgFile, true, true)
            ?? throw new ArgumentException("RGB image '" + imgFile + "' does not contains palette data (see 'Important note about image format' in the rescomp.txt file");

        int maxIndex = image.Length == 0 ? int.MinValue : image.Max(b => (int)b);
        if (maxIndex >= 64)
            throw new ArgumentException("'" + imgFile
                + "' uses color index >= 64, SPRITE resource requires image with a maximum of 64 colors, use 4bpp indexed colors image instead if you are unsure.");

        var info = ImageUtil.GetBasicInfo(imgFile);
        int w = info.W;
        int h = image.Length / w;

        int palIndex;
        try
        {
            palIndex = ImageUtil.GetSpritePaletteIndex(image, w, h);
        }
        catch (ArgumentException e)
        {
            throw new ArgumentException("'" + imgFile + "' SPRITE resource use more than 1 palette (16 colors), use 4bpp indexed colors image instead if you are unsure.", e);
        }

        int wt = w / 8;
        int ht = h / 8;
        if (wt % wf != 0)
            throw new ArgumentException("Error: '" + imgFile + "' width (" + w + ") is not a multiple of cell width (" + (wf * 8) + ").");
        if (ht % hf != 0)
            throw new ArgumentException("Error: '" + imgFile + "' height (" + h + ") is not a multiple of cell height (" + (hf * 8) + ").");

        Palette = (Palette)AddInternalResource(new Palette(id + "_palette", imgFile, palIndex * 16, 16, true));

        // 分割の確認用 (2 倍の画像に VDP スプライトの枠を描く)
        var rects = new List<(int, int, int, int)>();
        int numAnim = ht / hf;
        int yOff = 0;
        for (int i = 0; i < numAnim; i++)
        {
            var animation = new SpriteAnimation(id + "_animation" + i, image, wt, ht, i, wf, hf, time[Math.Min(time.Length - 1, i)], collision, compression,
                optType, optLevel, optDuplicate);

            if (!animation.IsEmpty)
            {
                animation = (SpriteAnimation)AddInternalResource(animation);

                if (showCut)
                {
                    int xOff = 0;
                    // RGB 画像は上の 32 ピクセルがパレット
                    int yMargin = info.Bpp > 8 ? 32 : 0;
                    foreach (var frame in animation.Frames)
                    {
                        foreach (var spr in frame.VdpSprites)
                            rects.Add(((xOff + spr.OffsetX) * 2, (yMargin + yOff + spr.OffsetY) * 2, (spr.Wt * 8 * 2) - 1, (spr.Ht * 8 * 2) - 1));
                        xOff += wf * 8;
                    }
                }

                MaxNumTile = Math.Max(MaxNumTile, animation.MaxNumTile);
                MaxNumSprite = Math.Max(MaxNumSprite, animation.MaxNumSprite);
                Animations.Add(animation);
            }
            yOff += hf * 8;
        }

        if (showCut)
            ImageUtil.SaveScaledWithRects(imgFile, JavaCompat.SetExtension(imgFile, "") + "_opt.png", rects);

        hc = wf ^ (hf << 8) ^ (MaxNumTile << 16) ^ (MaxNumSprite << 24) ^ JavaCompat.ListHashCode(Animations) ^ Palette.GetHashCode();
    }

    public override int InternalHashCode() => hc;
    public override bool InternalEquals(object obj) =>
        obj is Sprite s && Wf == s.Wf && Hf == s.Hf && MaxNumTile == s.MaxNumTile && MaxNumSprite == s.MaxNumSprite
        && Animations.SequenceEqual(s.Animations) && Palette.Equals(s.Palette);
    public override List<Bin> GetInternalBinResources() => new();
    public override string ToString() => Id + ": wf=" + Wf + " hf=" + Hf + " numAnim=" + Animations.Count + " maxNumTile=" + MaxNumTile + " maxNumSprite=" + MaxNumSprite;
    public override int ShallowSize() => (Animations.Count * 4) + 2 + 2 + 4 + 2 + 4 + 2 + 2;
    public override int TotalSize() => Animations.Sum(a => a.TotalSize()) + Palette.TotalSize() + ShallowSize();

    public override void Out(BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        outB.Reset();
        Util.Decl(outS, outH, null, Id + "_animations", 2, false);
        foreach (var animation in Animations)
            outS.Append("    dc.l    " + animation.Id + "\n");
        outS.Append("\n");

        Util.Decl(outS, outH, "SpriteDefinition", Id, 2, Global);
        outS.Append("    dc.w    " + (Wf * 8) + "\n");
        outS.Append("    dc.w    " + (Hf * 8) + "\n");
        outS.Append("    dc.l    " + Palette.Id + "\n");
        outS.Append("    dc.w    " + Animations.Count + "\n");
        outS.Append("    dc.l    " + Id + "_animations" + "\n");
        outS.Append("    dc.w    " + MaxNumTile + "\n");
        outS.Append("    dc.w    " + MaxNumSprite + "\n");
        outS.Append("\n");
    }
}
