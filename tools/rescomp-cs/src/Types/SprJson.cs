using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sgdk.Rescomp.Types;

// [EXT] SPRX: JSON sprite format (version 4) used by the SPRJ resource.
// "attributes" (parts / frames / animations / boxes) is user-defined data for future extensions: ignored for now.
// "loop" is an editor-side setting: ignored (SGDK animations always loop to frame 0).

public sealed class SprJsonDocument
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("coordinates")] public SprJsonCoordinates Coordinates { get; set; }
    [JsonPropertyName("canvas")] public SprJsonSize Canvas { get; set; }
    [JsonPropertyName("parts")] public List<SprJsonPart> Parts { get; set; } = new();
    [JsonPropertyName("frames")] public List<SprJsonFrame> Frames { get; set; } = new();
    [JsonPropertyName("animations")] public List<SprJsonAnimation> Animations { get; set; } = new();

    public const int SupportedVersion = 4;

    static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static SprJsonDocument Load(string file)
    {
        SprJsonDocument doc;
        try
        {
            doc = JsonSerializer.Deserialize<SprJsonDocument>(File.ReadAllText(file), options);
        }
        catch (JsonException e)
        {
            throw new ArgumentException("SPRJ: cannot parse JSON file '" + file + "': " + e.Message, e);
        }

        if (doc == null)
            throw new ArgumentException("SPRJ: empty JSON file '" + file + "'");

        doc.Validate(file);
        return doc;
    }

    void Validate(string file)
    {
        string where = "SPRJ '" + file + "': ";

        if (Version != SupportedVersion)
            Console.WriteLine("Warning: " + where + "version " + Version + " (supported version is " + SupportedVersion + ")");

        var c = Coordinates ?? new SprJsonCoordinates();
        void Check(string name, string value, string expected)
        {
            if (value != null && !value.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(where + "coordinates." + name + " = '" + value + "' is not supported (only '" + expected + "')");
        }
        Check("origin", c.Origin, "canvasCenter");
        Check("anchor", c.Anchor, "topLeft");
        Check("axisX", c.AxisX, "right");
        Check("axisY", c.AxisY, "down");
        Check("box", c.Box, "minCorner");

        var partIds = new HashSet<int>();
        foreach (var p in Parts)
        {
            if (!partIds.Add(p.Id))
                throw new ArgumentException(where + "duplicated part id " + p.Id);
            if (p.Source == null || string.IsNullOrEmpty(p.Source.Image))
                throw new ArgumentException(where + "part " + p.Id + " has no source image");
            int w = p.Source.Width > 0 ? p.Source.Width : p.Width;
            int h = p.Source.Height > 0 ? p.Source.Height : p.Height;
            if (w != p.Width || h != p.Height)
                throw new ArgumentException(where + "part " + p.Id + " size (" + p.Width + "x" + p.Height + ") differs from its source rectangle (" + w + "x" + h + ")");
            if (p.Width <= 0 || p.Height <= 0 || (p.Width & 7) != 0 || (p.Height & 7) != 0)
                throw new ArgumentException(where + "part " + p.Id + " size (" + p.Width + "x" + p.Height + ") must be a multiple of 8");
            if (p.Width > 32 || p.Height > 32)
                throw new ArgumentException(where + "part " + p.Id + " size (" + p.Width + "x" + p.Height + ") is above hardware sprite limit (32x32)");
        }

        var frameIdx = new HashSet<int>();
        foreach (var f in Frames)
        {
            if (!frameIdx.Add(f.Index))
                throw new ArgumentException(where + "duplicated frame index " + f.Index);
            if (f.Sprites.Count > 16)
                throw new ArgumentException(where + "frame " + f.Index + " uses " + f.Sprites.Count + " sprites (max = 16)");
            foreach (var s in f.Sprites)
                if (!partIds.Contains(s.Part))
                    throw new ArgumentException(where + "frame " + f.Index + " references unknown part " + s.Part);
            foreach (var b in f.Boxes)
            {
                if (string.IsNullOrEmpty(b.Kind))
                    throw new ArgumentException(where + "frame " + f.Index + " box '" + b.Name + "' has no kind");
                if (b.X < -128 || b.X > 127 || b.Y < -128 || b.Y > 127)
                    throw new ArgumentException(where + "frame " + f.Index + " box '" + b.Name + "' position (" + b.X + "," + b.Y + ") out of range (-128..127)");
                if (b.Width < 1 || b.Width > 255 || b.Height < 1 || b.Height > 255)
                    throw new ArgumentException(where + "frame " + f.Index + " box '" + b.Name + "' size (" + b.Width + "x" + b.Height + ") out of range (1..255)");
            }
        }

        foreach (var a in Animations)
        {
            if (a.Entries.Count == 0)
                throw new ArgumentException(where + "animation " + a.Index + " has no entry");
            if (a.Entries.Count > 255)
                throw new ArgumentException(where + "animation " + a.Index + " has " + a.Entries.Count + " entries (max = 255)");
            foreach (var e in a.Entries)
                if (!frameIdx.Contains(e.Frame))
                    throw new ArgumentException(where + "animation " + a.Index + " references unknown frame " + e.Frame);
        }

        if (Animations.Count == 0)
            throw new ArgumentException(where + "no animation");
    }

    public SprJsonPart GetPart(int id) => Parts.First(p => p.Id == id);
    public SprJsonFrame GetFrame(int index) => Frames.First(f => f.Index == index);
}

public sealed class SprJsonCoordinates
{
    [JsonPropertyName("origin")] public string Origin { get; set; }
    [JsonPropertyName("anchor")] public string Anchor { get; set; }
    [JsonPropertyName("axisX")] public string AxisX { get; set; }
    [JsonPropertyName("axisY")] public string AxisY { get; set; }
    [JsonPropertyName("box")] public string Box { get; set; }
}

public sealed class SprJsonSize
{
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
}

public sealed class SprJsonSource
{
    [JsonPropertyName("image")] public string Image { get; set; }
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
}

public sealed class SprJsonPart
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("source")] public SprJsonSource Source { get; set; }
}

public sealed class SprJsonSprite
{
    [JsonPropertyName("part")] public int Part { get; set; }
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("flipX")] public bool FlipX { get; set; }
    [JsonPropertyName("flipY")] public bool FlipY { get; set; }
    [JsonPropertyName("palette")] public int Palette { get; set; }
}

public sealed class SprJsonBox
{
    [JsonPropertyName("kind")] public string Kind { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
}

public sealed class SprJsonFrame
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("defaultDurationMs")] public double DefaultDurationMs { get; set; }
    [JsonPropertyName("sprites")] public List<SprJsonSprite> Sprites { get; set; } = new();
    [JsonPropertyName("boxes")] public List<SprJsonBox> Boxes { get; set; } = new();
}

public sealed class SprJsonAnimEntry
{
    [JsonPropertyName("frame")] public int Frame { get; set; }
    [JsonPropertyName("durationMs")] public double? DurationMs { get; set; }
}

public sealed class SprJsonAnimation
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("entries")] public List<SprJsonAnimEntry> Entries { get; set; } = new();
}
