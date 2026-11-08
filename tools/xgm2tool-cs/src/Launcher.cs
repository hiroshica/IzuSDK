namespace Isdk.Xgm2Tool;

/// <summary>xgm2tool のコマンドライン版 (Java 版の Launcher。GUI は移していない)。</summary>
public static class Launcher
{
    const string VERSION = "1.08";
    const int SYSTEM_AUTO = -1;
    const int SYSTEM_NTSC = 0;
    const int SYSTEM_PAL = 1;

    public static int sys = SYSTEM_AUTO;
    public static bool silent = false;
    public static bool verbose = false;
    public static bool sampleRateFix = true;
    public static bool sampleIgnore = true;
    public static bool sampleAdvancedCompare = false;
    public static bool delayKeyOff = false;

    /// <summary>-ac (指紋での比較) は移していないので、これが立ったら 3 を返して jar に任せる。</summary>
    public const int ExitNotPorted = 3;

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("GUI is not available in the C# xgm2tool (use xgm2tool.jar).");
            return ExitNotPorted;
        }
        if (args.Length < 2)
        {
            ShowUsage();
            return 2;
        }
        try
        {
            return Execute(args);
        }
        catch (NotPortedException e)
        {
            Console.Error.WriteLine(e.Message);
            return ExitNotPorted;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.ToString());
            return 1;
        }
    }

    static int Execute(string[] args)
    {
        var files = new List<string>();
        sys = SYSTEM_AUTO;
        silent = false;
        verbose = false;
        sampleIgnore = true;
        sampleRateFix = true;
        delayKeyOff = true;

        foreach (var arg in args)
        {
            if (StringUtil.equals(arg, "-s")) { silent = true; verbose = false; }
            else if (StringUtil.equals(arg, "-v")) { verbose = true; silent = false; }
            else if (StringUtil.equals(arg, "-di")) sampleIgnore = false;
            else if (StringUtil.equals(arg, "-dr")) sampleRateFix = false;
            else if (StringUtil.equals(arg, "-dd")) delayKeyOff = false;
            else if (StringUtil.equals(arg, "-ac")) sampleAdvancedCompare = true;
            else if (StringUtil.equals(arg, "-n")) sys = SYSTEM_NTSC;
            else if (StringUtil.equals(arg, "-p")) sys = SYSTEM_PAL;
            else if (arg.StartsWith("-")) Console.WriteLine("Warning: option '" + arg + "' not recognized (ignored)");
            else files.Add(arg);
        }

        if (silent) verbose = false;
        if (sampleAdvancedCompare)
            throw new NotPortedException("option -ac is not supported yet by the C# xgm2tool");

        int numFile = files.Count;
        string outFile = files[files.Count - 1];
        string outExt = FileExt(outFile).ToUpperInvariant();

        if (numFile > 2)
        {
            // 複数曲: VGM → XGM / XGC
            for (int i = 0; i < numFile - 1; i++)
            {
                string file = files[i];
                if (!File.Exists(file))
                {
                    Console.WriteLine("Error: the source file '" + file + "' could not be find");
                    return 2;
                }
                string ext = FileExt(file).ToUpperInvariant();
                if (!StringUtil.isEmpty(ext) & !StringUtil.equals(ext, "VGM"))
                {
                    Console.WriteLine("Error: expected VGM file as input for multi tracks conversion (found " + ext + ")");
                    return 2;
                }
            }
            if (!StringUtil.equals(outExt, "XGM") && !StringUtil.equals(outExt, "XGC"))
            {
                Console.WriteLine("Error: expected XGM or XGC file as output for multi tracks conversion (found " + outExt + ")");
                return 2;
            }

            var xgms = new List<XGM>();
            for (int i = 0; i < numFile - 1; i++)
                xgms.Add(new XGM(loadVGM(files[i]), StringUtil.equals(outExt, "XGC")));
            byte[] outData = new XGMMulti(xgms, StringUtil.equals(outExt, "XGC")).asByteArray();
            Util.writeBinaryFile(outData, outFile);
            return 0;
        }

        string inFile = files[0];
        if (!File.Exists(inFile))
        {
            Console.WriteLine("Error: the source file '" + inFile + "' could not be find");
            return 2;
        }
        string inExt = FileExt(inFile).ToUpperInvariant();

        if (StringUtil.equals(inExt, "VGM") || inExt.Length == 0)
        {
            VGM vgm = loadVGM(inFile);
            if (StringUtil.equals(outExt, "VGM"))
                Util.writeBinaryFile(vgm.asByteArray(), outFile);
            else
            {
                XGM xgm = new XGM(vgm, !StringUtil.equals(outExt, "XGM"));
                Util.writeBinaryFile(xgm.asByteArray(), outFile);
            }
        }
        else if (StringUtil.equals(inExt, "XGM"))
        {
            XGM xgm = loadXGM(inFile);
            if (StringUtil.equals(outExt, "VGM"))
                Util.writeBinaryFile(new VGM(xgm).asByteArray(), outFile);
            else
            {
                xgm.packed = true;
                Util.writeBinaryFile(xgm.asByteArray(), outFile);
            }
        }
        else if (StringUtil.equals(inExt, "XGC"))
        {
            XGM xgm = loadXGM(inFile);
            if (StringUtil.equals(outExt, "VGM"))
                Util.writeBinaryFile(new VGM(xgm).asByteArray(), outFile);
            else if (StringUtil.equals(outExt, "XGM"))
            {
                xgm.packed = false;
                Util.writeBinaryFile(xgm.asByteArray(), outFile);
            }
            else
            {
                Console.WriteLine("Error: the output file '" + outFile + "' is incorrect (should be a VGM or XGM file)");
                return 4;
            }
        }
        else
        {
            Console.WriteLine("Error: the input file '" + inFile + "' is incorrect (should be a VGM, XGM or XGC file)");
            return 4;
        }
        return 0;
    }

    /// <summary>FileUtil.getFileExtension(file, false): 最後の '.' より後ろ。</summary>
    static string FileExt(string path)
    {
        string name = path.Replace('\\', '/');
        name = name.Substring(name.LastIndexOf('/') + 1);
        int i = name.LastIndexOf('.');
        return i < 0 ? "" : name.Substring(i + 1);
    }

    static VGM loadVGM(string file)
    {
        byte[] inData = Util.readBinaryFile(file);
        if (sys == SYSTEM_NTSC) inData[0x24] = 60;
        else if (sys == SYSTEM_PAL) inData[0x24] = 50;
        return new VGM(inData, true);
    }

    static XGM loadXGM(string file) => new XGM(Util.readBinaryFile(file));

    static void ShowUsage()
    {
        Console.WriteLine("XGM2Tool " + VERSION + " - Stephane Dallongeville - copyright 2024");
        Console.WriteLine("");
        Console.WriteLine("Usage: xgm2tool inputFile(s) outputFile <options>");
    }
}

public sealed class NotPortedException : Exception
{
    public NotPortedException(string message) : base(message) { }
}
