using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Sgdk.Rescomp.Processors;
using Sgdk.Rescomp.Resources;

namespace Sgdk.Rescomp;

/// <summary>.res を読んで .s / .h / .d を作る (Java 版の sgdk.rescomp.Compiler)。</summary>
public static class Compiler
{
    const string ExtPostfix = "_ext.dll";

    static readonly List<IProcessor> resourceProcessors = new()
    {
        // 関数
        new AlignProcessor(),
        new UngroupProcessor(),
        new NearProcessor(),
        // リソース
        new BinProcessor(),
        new PaletteProcessor(),
        new BitmapProcessor(),
        new ImageProcessor(),
        new TilesetProcessor(),
        new TilemapProcessor(),
        new SpriteProcessor(),
        new MapProcessor(),
    };

    /// <summary>Java 版にあって、まだ C# に移していない種類。</summary>
    static readonly string[] notYetPorted = { "OBJECTS", "WAV", "XGM", "XGM2" };

    /// <summary>
    /// 種類ごとにまとめて出力する (圧縮が効くように) リソースの型。並びが出力の順番になる。
    /// Java 版の Tilemap・Tileset・Bitmap・Map の順。移したものから足していく。
    /// </summary>
    public static readonly List<Type> GroupedBinOwnerTypes = new() { typeof(Tilemap), typeof(Tileset), typeof(Bitmap), typeof(Map) };

    /// <summary>BIN より前に、圧縮できるメタデータとして出力する型 (Java 版の VDPSprite・Collision)。</summary>
    public static readonly List<Type> EarlyMetadataTypes = new() { typeof(VDPSprite), typeof(Collision) };

    /// <summary>まとめ方が特別な型 (Java 版の Map) のために、BIN の集め方を差し替える。</summary>
    public static readonly Dictionary<Type, Func<List<Resource>, bool, IEnumerable<Bin>>> CustomBinCollectors = new() { [typeof(Map)] = Map.CollectBins };

    /// <summary>集計で「スプライトのメタデータ」として数える型。</summary>
    public static readonly List<Type> SpriteMetadataTypes = new() { typeof(Sprite), typeof(SpriteFrame), typeof(SpriteAnimation) };

    /// <summary>行を読み終えたあとに呼ぶもの (Java 版の TMX のオブジェクト参照の解決など)。</summary>
    public static readonly List<Action<List<Resource>>> PostProcessors = new();

    public static string CurrentDir;
    public static string ResDir;

    static readonly Dictionary<Resource, Resource> resources = new();
    static readonly List<Resource> resourcesList = new();
    static readonly List<string> resourcesFile = new();
    static readonly HashSet<string> resourcesFileSet = new(StringComparer.Ordinal);

    static bool extensionsLoaded;

    /// <summary>まだ C# に移していない種類があって止まったら true (Program は終了コード 3 を返す)。</summary>
    public static bool UnsupportedType;

    public static IReadOnlyList<Resource> ResourcesList => resourcesList;

    public static bool Compile(string fileName, string fileNameOut, bool asm, bool header, string depTarget)
    {
        CurrentDir = AppContext.BaseDirectory;
        ResDir = JavaCompat.GetDirectory(fileName);

        resources.Clear();
        resourcesList.Clear();
        resourcesFile.Clear();
        resourcesFileSet.Clear();

        string[] lines;
        try
        {
            lines = File.ReadAllLines(fileName, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Couldn't open input file " + Path.GetFullPath(fileName) + ":");
            Console.Error.WriteLine(e.Message);
            return false;
        }

        int lineCnt = 0;
        int align = -1;
        bool group = true;
        bool near = false;

        foreach (var l in lines)
        {
            var line = Regex.Replace(JavaCompat.Trim(l).Replace("\t", " "), " +", " ");
            lineCnt++;

            if (line.Length == 0)
                continue;
            if (line.StartsWith("//") || line.StartsWith("#"))
                continue;

            var resource = Execute(line);
            if (resource == null)
            {
                Console.Error.WriteLine(fileName + ": error on line " + lineCnt);
                return false;
            }

            switch (resource)
            {
                case Align a:
                    align = a.AlignValue;
                    Console.WriteLine();
                    break;
                case Ungroup:
                    group = false;
                    Console.WriteLine();
                    break;
                case Near:
                    near = true;
                    Console.WriteLine();
                    break;
                default:
                    AddResource(resource);
                    Console.WriteLine(" '" + resource.Id + "' raw size: " + resource.TotalSize() + " bytes");
                    if (resource.GetType().Name == "Map")
                        Console.WriteLine(resource.ToString());
                    break;
            }
        }

        foreach (var post in PostProcessors)
            post(resourcesList);

        Console.WriteLine();

        var outS = new StringBuilder(1024);
        var outH = new StringBuilder(1024);

        try
        {
            var outB = new BinaryStream();

            // ヘッダの名前は .res のあるフォルダの名前から
            string headerName = JavaCompat.GetFileName(JavaCompat.GetDirectory(ResDir, false), false);
            if (string.IsNullOrEmpty(headerName))
                headerName = "RES";
            headerName += "_" + JavaCompat.GetFileName(fileNameOut, false);
            headerName = headerName.ToUpperInvariant();

            outH.Append("#include <genesis.h>\n\n");
            outH.Append("#ifndef _" + headerName + "_H_\n");
            outH.Append("#define _" + headerName + "_H_\n\n");

            var binResources = GetBinResources(false);
            var farBinResources = GetBinResources(true);
            var binResourcesOfPalette = GetInternalBinResourcesOf(null, typeof(Palette), false);
            var groupedInternalBinResources = new List<Bin>();
            var farGroupedInternalBinResources = new List<Bin>();

            if (group)
            {
                foreach (var t in GroupedBinOwnerTypes)
                    GetInternalBinResourcesOf(groupedInternalBinResources, t, false);
                RemoveAll(groupedInternalBinResources, binResourcesOfPalette);
                foreach (var t in GroupedBinOwnerTypes)
                    GetInternalBinResourcesOf(farGroupedInternalBinResources, t, true);
            }

            RemoveAll(binResources, binResourcesOfPalette);
            RemoveAll(binResources, groupedInternalBinResources);
            RemoveAll(farBinResources, farGroupedInternalBinResources);

            var nonBinResources = resourcesList.Where(r => r is not Bin).ToList();
            var earlyResources = EarlyMetadataTypes.Select(GetResources).ToList();
            foreach (var early in earlyResources)
                RemoveAll(nonBinResources, early);

            // バイナリを先に出力する (メタデータが BIN の圧縮の結果を使うため)
            outS.Append(".section .rodata\n\n");
            outB.Reset();
            foreach (var early in earlyResources)
                ExportResources(early, outB, outS, outH);

            outS.Append(".section .rodata_bin\n\n");
            outB.Reset();
            ExportResources(binResourcesOfPalette, outB, outS, outH);
            ExportResources(binResources, outB, outS, outH);
            ExportResources(groupedInternalBinResources, outB, outS, outH);

            if (!near)
            {
                outS.Append(".section .rodata_binf\n\n");
                outB.Reset();
            }

            if (align != -1)
            {
                outS.Append("    .align  " + align + "\n\n");
                outB.Reset();
            }

            ExportResources(farBinResources, outB, outS, outH);
            ExportResources(farGroupedInternalBinResources, outB, outS, outH);

            outS.Append(".section .rodata\n\n");
            outB.Reset();
            ExportResources(nonBinResources, outB, outS, outH);

            outH.Append("\n");
            outH.Append("#endif // _" + headerName + "_H_\n");

            int unpackedSize = 0, packedRawSize = 0, packedSize = 0;
            foreach (var bin in resourcesList.OfType<Bin>())
            {
                if (bin.DoneCompression != Compression.NONE)
                {
                    packedRawSize += bin.Data.Length + (bin.Data.Length & 1);
                    packedSize += bin.PackedData.Data.Length + (bin.PackedData.Data.Length & 1);
                }
                else
                    unpackedSize += bin.Data.Length + (bin.Data.Length & 1);
            }

            Console.WriteLine();
            Console.WriteLine(fileName + " summary:");
            Console.WriteLine("-------------");
            Console.WriteLine("Binary data: " + (unpackedSize + packedSize) + " bytes");
            if (unpackedSize > 0)
                Console.WriteLine("  Unpacked: " + unpackedSize + " bytes");
            if (packedSize > 0)
                Console.WriteLine("  Packed: " + packedSize + " bytes (" + JavaCompat.Round((packedSize * 100f) / packedRawSize) + "% - origin size: " + packedRawSize + " bytes)");

            var allSprites = SpriteMetadataTypes.SelectMany(GetResources).ToList();
            foreach (var early in earlyResources)
                foreach (var r in early)
                    if (!allSprites.Contains(r))
                        allSprites.Add(r);
            int spriteMetaSize = allSprites.Sum(r => r.ShallowSize());
            if (spriteMetaSize > 0)
                Console.WriteLine("Sprite metadata (all but tiles and palette data): " + spriteMetaSize + " bytes");

            RemoveAll(nonBinResources, allSprites);
            int miscMetaSize = nonBinResources.Sum(r => r.ShallowSize());
            if (miscMetaSize > 0)
                Console.WriteLine("Misc metadata (map, bitmap, image, tilemap, tileset, palette..): " + miscMetaSize + " bytes");

            int totalSize = unpackedSize + packedSize + spriteMetaSize + miscMetaSize;
            Console.WriteLine("Total: " + totalSize + " bytes (" + JavaCompat.Round(totalSize / 1024d) + " KB)");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(e);
            return false;
        }

        try
        {
            if (asm)
                WriteText(fileNameOut, outS.ToString());
            if (header)
                WriteText(JavaCompat.SetExtension(fileNameOut, ".h"), outH.ToString());
            if (depTarget != null)
                WriteText(JavaCompat.SetExtension(fileNameOut, ".d"), GenerateDependency(fileName, depTarget));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Couldn't create output file:");
            Console.Error.WriteLine(e.Message);
            return false;
        }

        return true;
    }

    static void WriteText(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));

    // Java の List.removeAll と同じ (equals で比べて、含まれるものをすべて除く)
    static void RemoveAll<T>(List<T> list, IEnumerable<T> remove) where T : Resource
    {
        var set = remove.ToList();
        list.RemoveAll(x => set.Any(r => r.Equals(x)));
    }

    static string GetFixedPath(string path) => JavaCompat.GenericPath(path).Replace(" ", "\\ ");

    static string GenerateDependency(string resFileName, string targetFileName)
    {
        var result = new StringBuilder(GetFixedPath(resFileName));
        foreach (var f in JavaCompat.JavaHashSetOrder(resourcesFile))
            result.Append(" \\\n").Append(GetFixedPath(f));
        return GetFixedPath(targetFileName) + ": " + result;
    }

    public static List<Resource> GetResources(Type type) =>
        resourcesList.Where(r => type == null || type.IsInstanceOfType(r)).ToList();

    static List<Bin> GetBinResources(bool far) =>
        resourcesList.OfType<Bin>().Where(b => b.Far == far).ToList();

    static List<Bin> GetInternalBinResourcesOf(List<Bin> dest, Type from, bool far)
    {
        var result = dest ?? new List<Bin>();
        IEnumerable<Bin> bins = CustomBinCollectors.TryGetValue(from, out var collect)
            ? collect(GetResources(from), far)
            : GetResources(from).SelectMany(r => r.GetInternalBinResources()).Where(b => b.Far == far);
        foreach (var bin in bins)
            if (!result.Contains(bin))
                result.Add(bin);
        return result;
    }

    static void ExportResources(IEnumerable<Resource> list, BinaryStream outB, StringBuilder outS, StringBuilder outH)
    {
        foreach (var res in list)
            res.Out(outB, outS, outH);
    }

    public static Resource FindResource(Resource resource) =>
        resources.TryGetValue(resource, out var r) ? r : null;

    public static Resource AddResource(Resource resource, bool internalRes)
    {
        if (internalRes)
        {
            var result = FindResource(resource);
            if (result != null)
            {
                Console.WriteLine("Info: '" + resource.Id + "' has same content as '" + result.Id + "'");
                return result;
            }
            resource.Global = false;
        }

        // Java の HashMap.put と同じく、同じ中身のキーがあれば値だけ置き換える
        resources[resource] = resource;
        resourcesList.Add(resource);
        return resource;
    }

    public static Resource AddResource(Resource resource) => AddResource(resource, false);

    /// <summary>依存ファイル (.d) に書くファイルを加える。</summary>
    public static void AddResourceFile(string file)
    {
        if (resourcesFileSet.Add(file))
            resourcesFile.Add(file);
    }

    public static Resource GetResourceById(string id)
    {
        if (id == "NULL")
            return null;
        return resourcesList.FirstOrDefault(r => r.Id == id);
    }

    static Resource Execute(string input)
    {
        try
        {
            // 引用符の中の空白では分けない
            var fields = SplitLikeJava(input);
            for (int f = 0; f < fields.Count; f++)
                fields[f] = fields[f].Replace("\"", "");

            string type = fields[0];
            var processor = GetResourceProcessor(type);

            if (processor == null)
            {
                if (notYetPorted.Contains(type.ToUpperInvariant()))
                {
                    Console.Error.WriteLine("Error: resource type '" + type + "' is not supported yet by the C# rescomp (use rescomp.jar for now)");
                    UnsupportedType = true;
                    return null;
                }

                Console.Error.WriteLine("Error: unknown resource type '" + type + "'");
                Console.WriteLine("Accepted resource types are:");
                foreach (var rp in resourceProcessors)
                    Console.WriteLine("  " + rp.Id);
                return null;
            }

            Console.WriteLine("Resource: " + input);
            Console.WriteLine("--> executing plugin " + type + "...");

            return processor.Execute(fields.ToArray());
        }
        catch (NotPortedException e)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Error: " + e.Message + " (use rescomp.jar for now)");
            UnsupportedType = true;
            return null;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("Error: cannot compile resource '" + input + "'");
            Console.Error.WriteLine(e);
            return null;
        }
    }

    /// <summary>input.split(" +(?=(([^\"]*\"){2})*[^\"]*$)") と同じ: 引用符の外の空白で分ける。</summary>
    static List<string> SplitLikeJava(string input)
    {
        var result = new List<string>();
        var re = new Regex(" +(?=(?:(?:[^\"]*\"){2})*[^\"]*$)");
        int last = 0;
        foreach (Match m in re.Matches(input))
        {
            // 先頭で幅のある区切りが見つかったときも、Java は空の要素を作る
            result.Add(input.Substring(last, m.Index - last));
            last = m.Index + m.Length;
        }
        result.Add(input.Substring(last));
        while (result.Count > 1 && result[^1].Length == 0)
            result.RemoveAt(result.Count - 1);
        return result;
    }

    static IProcessor GetResourceProcessor(string type)
    {
        var rp = resourceProcessors.FirstOrDefault(p => type.Equals(p.Id, StringComparison.OrdinalIgnoreCase));
        if (rp != null)
            return rp;

        if (!extensionsLoaded)
        {
            extensionsLoaded = true;
            LoadExtensions();
            rp = resourceProcessors.FirstOrDefault(p => type.Equals(p.Id, StringComparison.OrdinalIgnoreCase));
        }
        return rp;
    }

    /// <summary>拡張を登録する (*_ext.dll から読んだものや、テストから直接)。同じ名前の種類がすでにあれば false。</summary>
    public static bool RegisterProcessor(IProcessor processor, string source = null)
    {
        var id = processor.Id;
        if (id == null)
        {
            Console.Error.WriteLine("WARNING: Extension: '" + source + "' loading failed. Class '" + processor.GetType().FullName + "' returned null from Id.");
            return false;
        }
        if (resourceProcessors.Any(p => id.Equals(p.Id)))
        {
            Console.WriteLine("WARNING: Extension: '" + source + "' loading skiped. Resource type: '" + id + "' already exists!");
            return false;
        }
        resourceProcessors.Add(processor);
        if (source != null)
            Console.WriteLine("Extension: '" + source + "' loaded. '" + id + "' resource type support added.");
        return true;
    }

    /// <summary>.res と同じフォルダ (なければ今のフォルダ) の *_ext.dll から IProcessor を読む。</summary>
    static void LoadExtensions()
    {
        var dir = string.IsNullOrEmpty(ResDir) ? "." : ResDir;
        string[] files;
        try
        {
            files = Directory.GetFiles(dir).Where(f => f.EndsWith(ExtPostfix, StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            Type[] types;
            try
            {
                var asm = Assembly.LoadFrom(Path.GetFullPath(file));
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("WARNING: Extension: '" + name + "' loading failed. Error processing DLL file: " + e.Message);
                continue;
            }

            foreach (var t in types)
            {
                if (!typeof(IProcessor).IsAssignableFrom(t) || t.IsAbstract || t.IsInterface)
                    continue;
                try
                {
                    var processor = (IProcessor)Activator.CreateInstance(t);
                    RegisterProcessor(processor, name);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("WARNING: Extension: '" + name + "' loading failed. Class '" + t.FullName + " is a Processor but cannot be instantiated: " + e.Message);
                }
            }
        }
    }
}
