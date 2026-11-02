using Sgdk.Rescomp.Imaging;
using Sgdk.Rescomp.Resources;
using Sgdk.Rescomp.Types;

namespace Sgdk.Rescomp.Processors;

public sealed class SpriteProcessor : IProcessor
{
    public string Id => "SPRITE";

    static void WrongDefinition()
    {
        Console.WriteLine("Wrong SPRITE definition");
        Console.WriteLine("SPRITE name \"file\" width height [compression [time [collision [opt_type [opt_level]]]]]");
    }

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 5)
        {
            Console.WriteLine("Wrong SPRITE definition");
            Console.WriteLine("SPRITE name \"file\" width height [compression [time [collision [opt_type [opt_level [opt_duplicate]]]]]]");
            Console.WriteLine("  name          Sprite variable name");
            Console.WriteLine("  file          the image file to convert to SpriteDefinition structure (BMP or PNG image)");
            Console.WriteLine("  width         width of a single sprite frame in tile (should be < 32), or in pixels (should be a multiple of 8 with 'p' postfix, like 32p or 32P), or number of frames in width (with 'f' postfix, like 4f or 4F)");
            Console.WriteLine("  height        height of a single sprite frame in tile (should be < 32), or in pixels (should be a multiple of 8 with 'p' postfix, like 16p or 16P), or number of frames in height (with 'f' postfix, like 1f or 1F)");
            Console.WriteLine("  compression   compression type, accepted values:");
            Console.WriteLine("                   -1 / BEST / AUTO = use best compression");
            Console.WriteLine("                    0 / NONE        = no compression (default)");
            Console.WriteLine("                    1 / APLIB       = aplib library (good compression ratio but slow)");
            Console.WriteLine("                    2 / FAST / LZ4W = custom lz4 compression (average compression ratio but fast)");
            Console.WriteLine("  time          display frame time in 1/60 of second (time between each animation frame)");
            Console.WriteLine("                    If this value is set to 0 (default) then auto animation is disabled");
            Console.WriteLine("                    It can be set globally (single value) or independently for each frame of each animation");
            Console.WriteLine("                    Example for a sprite sheet of 3 animations x 5 frames:");
            Console.WriteLine("                    [[3,3,3,4,4][4,5,5][2,3,3,4]]");
            Console.WriteLine("                    As you can see you can have empty value for empty frame");
            Console.WriteLine("  collision     collision type: CIRCLE, BOX or NONE (BOX by default)");
            Console.WriteLine("  opt_type      sprite cutting optimization strategy, accepted values:");
            Console.WriteLine("                    0 / BALANCED  = balance between used tiles and hardware sprites (default)");
            Console.WriteLine("                    1 / SPRITE    = reduce the number of hardware sprite (using bigger sprite) at the expense of more used tiles");
            Console.WriteLine("                    2 / TILE      = reduce the number of tiles at the expense of more hardware sprite (using smaller sprite)");
            Console.WriteLine("                    3 / NONE      = no optimization (cover the whole sprite frame)");
            Console.WriteLine("  opt_level     optimization level for the sprite cutting operation:");
            Console.WriteLine("                    FAST      = fast optimisation, good enough in general (default)");
            Console.WriteLine("                    MEDIUM    = intermediate optimisation level, provide better results than FAST but ~5 time slower");
            Console.WriteLine("                    SLOW      = advanced optimisation level using a genetic algorithm (80000 iterations), ~20 time slower than FAST");
            Console.WriteLine("                    MAX       = maximum optimisation level, genetic algorithm (500000 iterations), ~100 time slower than FAST");
            Console.WriteLine("  opt_duplicate enabled optimization of consecutive duplicated frames by removing them and increasing animation time to compensante.");
            Console.WriteLine("                    FALSE     = no optimization (default)");
            Console.WriteLine("                                Note that duplicated frames pixel data are still removed by rescomp binary blob optimizer");
            Console.WriteLine("                    TRUE      = only the first instance of consecutive duplicated frames is kept and 'timer' value is increased to compensate the removed frames time.");
            Console.WriteLine("                                Note that it *does* change the 'animation.numFrame' information so beware of that when enabling this optimization.");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        string wArg = fields[3];
        string hArg = fields[4];

        var info = ImageUtil.GetBasicInfo(fileIn);

        // 幅: "64P" はピクセル、"4F" はフレームの数、数字だけならタイル
        int wf;
        if (JavaCompat.IsTaggedNumber(wArg.ToUpperInvariant(), "P"))
        {
            int pix = JavaCompat.ParseTaggedInt(wArg.ToUpperInvariant(), "P", 0);
            if (pix % 8 != 0)
                throw new ArgumentException("Error: the Sprite '" + id + "' width parameter (" + pix + " pixels), is not a multiple of 8");
            wf = pix / 8;
        }
        else if (JavaCompat.IsTaggedNumber(wArg.ToUpperInvariant(), "F"))
        {
            int fc = JavaCompat.ParseTaggedInt(wArg.ToUpperInvariant(), "F", 0);
            if (info.W % fc != 0)
                throw new ArgumentException("Error: '" + fileIn + "' width (" + info.W + ") is not a multiple of frame count (" + fc + ")");
            wf = info.W / fc / 8;
        }
        else
            wf = JavaCompat.ParseInt(wArg, 0);

        int hf;
        if (JavaCompat.IsTaggedNumber(hArg.ToUpperInvariant(), "P"))
        {
            int pix = JavaCompat.ParseTaggedInt(hArg.ToUpperInvariant(), "P", 0);
            if (pix % 8 != 0)
                throw new ArgumentException("Error: the Sprite '" + id + "' height parameter (" + pix + " pixels), is not a multiple of 8");
            hf = pix / 8;
        }
        else if (JavaCompat.IsTaggedNumber(hArg.ToUpperInvariant(), "F"))
        {
            int fc = JavaCompat.ParseTaggedInt(hArg.ToUpperInvariant(), "F", 0);
            if (info.H % fc != 0)
                throw new ArgumentException("Error: '" + fileIn + "' height (" + info.H + ") is not a multiple of frame count (" + fc + ")");
            hf = info.H / fc / 8;
        }
        else
            hf = JavaCompat.ParseInt(hArg, 0);

        if (wf < 1 || hf < 1 || wf >= 32 || hf >= 32)
        {
            WrongDefinition();
            return null;
        }

        var compression = fields.Length >= 6 ? Util.GetCompression(fields[5]) : Compression.NONE;
        var time = fields.Length >= 7 ? JavaCompat.ParseIntArray2D(fields[6], new[] { new[] { 0 } }) : new[] { new[] { 0 } };
        var collision = fields.Length >= 8 ? Util.GetCollision(fields[7]) : CollisionType.NONE;
        var opt = fields.Length >= 9 ? Util.GetSpriteOptType(fields[8]) : OptimizationType.BALANCED;
        var optLevel = OptimizationLevel.FAST;
        bool showCut = false;
        if (fields.Length >= 10)
        {
            optLevel = Util.GetSpriteOptLevel(fields[9]);
            showCut = true;
        }
        // Boolean.parseBoolean: "true" (大文字小文字を区別しない) だけが true
        bool optDuplicate = fields.Length >= 11 && fields[10].Equals("true", StringComparison.OrdinalIgnoreCase);

        Compiler.AddResourceFile(fileIn);
        return new Sprite(id, fileIn, wf, hf, compression, time, collision, opt, optLevel, showCut, optDuplicate);
    }
}
