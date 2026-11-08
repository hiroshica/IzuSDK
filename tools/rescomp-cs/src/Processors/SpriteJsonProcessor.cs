using System.Globalization;
using Sgdk.Rescomp.Resources;
using Sgdk.Rescomp.Types;

namespace Sgdk.Rescomp.Processors;

// [EXT] SPRX: JSON sprite resource (extended frames, see inc/ext/sprite_eng_ext.h).

public sealed class SpriteJsonProcessor : IProcessor
{
    public string Id => "SPRJ";

    public const double DefaultTimeDiv = 1000.0 / 60.0;

    static void Usage()
    {
        Console.WriteLine("Wrong SPRJ definition");
        Console.WriteLine("SPRJ name \"json\" vram_mode [compression [timediv [png]]]");
        Console.WriteLine("  name          SprxInfo variable name (also generates name_def (SpriteDefinition) and name_palette (Palette))");
        Console.WriteLine("  json          JSON sprite file (version 4)");
        Console.WriteLine("  vram_mode     required, how part tiles are put in VRAM:");
        Console.WriteLine("                    RESIDENT = all part tiles loaded once (SPRX_loadResident), shared by all sprites");
        Console.WriteLine("                    TRANSFER = frame tiles uploaded on each frame change (VRAM = max tiles of a frame per sprite)");
        Console.WriteLine("  compression   NONE (default) / APLIB / FAST / BEST (NONE or FAST recommended for TRANSFER)");
        Console.WriteLine("  timediv       durationMs is divided by this value to get the frame time (in frames)");
        Console.WriteLine("                    default = 16.6667 (1000/60, 100ms -> 6), 20 = PAL real time, 0 = no auto animation");
        Console.WriteLine("  png           image used for parts whose source.image is not a file name (ex: \"(DotArt)\")");
        Console.WriteLine("                    either a single file, or name=file pairs separated by ',' (ex: \"DotArt=a.png,Other=b.png\")");
        Console.WriteLine("SPRJ_KINDS \"file\"  (optional, before any SPRJ) fixes the box kind enum order: one kind per line");
    }

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 4)
        {
            Usage();
            return null;
        }

        string id = fields[1];
        string jsonFile = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);

        SprxVramMode mode;
        switch (fields[3].ToUpperInvariant())
        {
            case "RESIDENT": mode = SprxVramMode.RESIDENT; break;
            case "TRANSFER": mode = SprxVramMode.TRANSFER; break;
            default:
                Usage();
                throw new ArgumentException("SPRJ '" + id + "': vram_mode is required and must be RESIDENT or TRANSFER (got '" + fields[3] + "')");
        }

        var compression = fields.Length >= 5 ? Util.GetCompression(fields[4]) : Compression.NONE;

        double timeDiv = DefaultTimeDiv;
        if (fields.Length >= 6 && fields[5].Length > 0)
        {
            if (!double.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out timeDiv) || timeDiv < 0)
                throw new ArgumentException("SPRJ '" + id + "': invalid timediv '" + fields[5] + "'");
        }

        string pngSingle = null;
        var pngMap = new Dictionary<string, string>(StringComparer.Ordinal);
        if (fields.Length >= 7 && fields[6].Length > 0)
        {
            if (fields[6].Contains('='))
            {
                foreach (var pair in fields[6].Split(','))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0)
                        throw new ArgumentException("SPRJ '" + id + "': invalid png mapping '" + pair + "'");
                    pngMap[pair.Substring(0, eq).Trim()] = JavaCompat.AdjustPath(Compiler.ResDir, pair.Substring(eq + 1).Trim());
                }
            }
            else
                pngSingle = JavaCompat.AdjustPath(Compiler.ResDir, fields[6]);
        }

        if (!File.Exists(jsonFile))
            throw new ArgumentException("SPRJ '" + id + "': file '" + jsonFile + "' not found");

        Compiler.AddResourceFile(jsonFile);
        var doc = SprJsonDocument.Load(jsonFile);
        string jsonDir = JavaCompat.GetDirectory(jsonFile);

        string ResolveImage(string name)
        {
            string key = name.Trim();
            string bare = key.StartsWith("(") && key.EndsWith(")") ? key.Substring(1, key.Length - 2) : key;

            if (pngMap.TryGetValue(key, out var f) || pngMap.TryGetValue(bare, out f))
                return CheckFile(f, name);

            // a real file name: relative to the JSON file, then to the .res
            if (Path.HasExtension(key) && !key.StartsWith("("))
            {
                var p = JavaCompat.AdjustPath(jsonDir, key);
                if (File.Exists(p)) return p;
                p = JavaCompat.AdjustPath(Compiler.ResDir, key);
                if (File.Exists(p)) return p;
                throw new ArgumentException("SPRJ '" + id + "': image '" + key + "' not found (relative to JSON or .res folder)");
            }

            if (pngSingle != null)
                return CheckFile(pngSingle, name);

            throw new ArgumentException("SPRJ '" + id + "': part image '" + name + "' is not a file name, give the png parameter in the .res");
        }

        string CheckFile(string f, string name)
        {
            if (!File.Exists(f))
                throw new ArgumentException("SPRJ '" + id + "': image file '" + f + "' (for '" + name + "') not found");
            return f;
        }

        return new SprxSpriteRes(id, jsonFile, doc, mode, compression, timeDiv, ResolveImage);
    }
}

public sealed class SpriteJsonKindsProcessor : IProcessor
{
    public string Id => "SPRJ_KINDS";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 2)
        {
            Console.WriteLine("Wrong SPRJ_KINDS definition");
            Console.WriteLine("SPRJ_KINDS \"file\"  (one box kind per line, declared order = enum value)");
            return null;
        }

        string file = JavaCompat.AdjustPath(Compiler.ResDir, fields[1]);
        if (!File.Exists(file))
            throw new ArgumentException("SPRJ_KINDS: file '" + file + "' not found");
        Compiler.AddResourceFile(file);
        return SprxKinds.FromFile(file);
    }
}
