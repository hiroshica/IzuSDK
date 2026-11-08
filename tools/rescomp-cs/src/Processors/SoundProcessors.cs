using System.Diagnostics;
using Isdk.Rescomp.Resources;

namespace Isdk.Rescomp.Processors;

public sealed class WavProcessor : IProcessor
{
    public string Id => "WAV";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 4)
        {
            Console.WriteLine("Wrong WAV definition");
            Console.WriteLine("WAV name \"file\" driver [out_rate [far]]");
            Console.WriteLine("  name      variable name");
            Console.WriteLine("  file      path of the .wav file (will be automatically converted int the correct format)");
            Console.WriteLine("  driver    specify the Z80 driver we will use to play the WAV file:");
            Console.WriteLine("              PCM / DEFAULT (0 value is not anymore accepted)");
            Console.WriteLine("                Single PCM channel sound driver.");
            Console.WriteLine("                It can play a sample (8 bit signed) from 8 Khz up to 32 Khz rate.");
            Console.WriteLine("                Method to use: SND_PCM_startPlay(..)");
            Console.WriteLine("              2DPCM (2ADPCM and 1 are not anymore accepted)");
            Console.WriteLine("                2 PCM channels DPCM sound driver.");
            Console.WriteLine("                It can mix up to 2 (4 bit) DCPM samples at a fixed 22050 Hz rate.");
            Console.WriteLine("                Method to use: SND_DPCM2_startPlay(..)");
            Console.WriteLine("              4PCM (2 and 3 are not anymore accepted)");
            Console.WriteLine("                4 PCM channels sample sound driver with volume support.");
            Console.WriteLine("                It can mix up to 4 samples (8 bit signed) at a fixed 16 Khz rate with 16 levels of volume.");
            Console.WriteLine("                Method to use: SND_PCM4_startPlay(..)");
            Console.WriteLine("              XGM (4 and 5 are not anymore accepted)");
            Console.WriteLine("                XGM music sound driver.");
            Console.WriteLine("                It can mix up to 4 samples (8 bit signed) at a fixed 14 Khz rate while playing XGM music.");
            Console.WriteLine("                Methods to use: XGM_setPCM(..) and XGM_startPlayPCM(..)");
            Console.WriteLine("              XGM2");
            Console.WriteLine("                XGM music sound driver.");
            Console.WriteLine("                It can mix up to 3 samples (8 bit signed) at either 13.3 Khz or 6.65 Khz while playing XGM music.");
            Console.WriteLine("                Methods to use: XGM2_playPCM(..)");
            Console.WriteLine("  out_rate  output PCM rate, this parameter is meaningful only for PCM and XGM2 driver.");
            Console.WriteLine("              PCM driver accepts following values: 8000, 11025, 13400, 16000, 22050, 32000 (default is 16000 if omitted)");
            Console.WriteLine("              XGM2 driver accepts only 6650 or 13300 (default is 13300 if omitted).");
            Console.WriteLine("  far       'far' binary data flag to put it at the end of the ROM (useful for bank switch, default = FALSE)");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        var driver = Util.GetSoundDriver(fields[3]);

        int outRate = driver switch
        {
            SoundDriver.PCM => 16000,
            SoundDriver.DPCM2 => 22050,
            SoundDriver.PCM4 => 16000,
            SoundDriver.XGM => 14000,
            SoundDriver.XGM2 => 13300,
            _ => 0,
        };
        if (fields.Length >= 5)
            outRate = JavaCompat.ParseInt(fields[4], outRate);
        bool far = false;
        if (fields.Length >= 6)
            far = JavaCompat.ParseBoolean(fields[5], far);

        byte[] pcmData = Sound.JavaSound.GetRawDataFromWav(fileIn, outRate);

        if (driver == SoundDriver.DPCM2)
            pcmData = Util.DpcmPack(pcmData);

        Compiler.AddResourceFile(fileIn);

        bool dpcm = driver == SoundDriver.DPCM2;
        return new Bin(id, pcmData, dpcm ? 128 : 256, dpcm ? 128 : 256, dpcm ? 136 : 0, Compression.NONE, far);
    }
}

public sealed class XgmProcessor : IProcessor
{
    public string Id => "XGM";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong XGM definition");
            Console.WriteLine("XGM name file [timing [options]]");
            Console.WriteLine("  name      XGM music variable name");
            Console.WriteLine("  file      path of the .vgm or .xgm music file to convert to binary data array");
            Console.WriteLine("  timing    define the XGM base timing");
            Console.WriteLine("               -1 (default) = AUTO (NTSC or PAL depending the information in source VGM file)");
            Console.WriteLine("                0 = NTSC (XGM is generated for NTSC system)");
            Console.WriteLine("                1 = PAL (XGM is generated for PAL system)");
            Console.WriteLine("  options   optional(s) parameter(s) for xgmtool");
            Console.WriteLine("                ex: \"-dr -di\" to disable some sample auto process (see xgmtool to get more info)");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        int timing = fields.Length >= 4 ? JavaCompat.ParseInt(fields[3], -1) : -1;
        string options = fields.Length >= 5 ? fields[4] : "";
        string fileOut = JavaCompat.SetExtension(fileIn, ".bin");

        if (!ExternalTools.Xgmtool(fileIn, fileOut, timing, options))
            throw new IOException("Error while compiling file '" + fileIn + "' to BIN format");

        byte[] data = File.Exists(fileOut) ? File.ReadAllBytes(fileOut) : null;
        try { File.Delete(fileOut); } catch (IOException) { }
        if (data == null)
            throw new IOException("Can't read BIN data from file'" + fileOut + "' !");

        Compiler.AddResourceFile(fileIn);
        return new Bin(id, data, 256, 256, 0, Compression.NONE, true);
    }
}

public sealed class Xgm2Processor : IProcessor
{
    public string Id => "XGM2";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 3)
        {
            Console.WriteLine("Wrong XGM2 definition");
            Console.WriteLine("Syntax for single track XGM:");
            Console.WriteLine("XGM2 name file(s) [options]");
            Console.WriteLine("  name      XGM2 music variable name");
            Console.WriteLine("  file      path of the .vgm music file to convert in compiled XGM2 file");
            Console.WriteLine("  options   optional(s) parameter(s) for xgm2Tool");
            Console.WriteLine("                Should always start with '-' character otherwise it won't be recognized");
            Console.WriteLine("                ex: \"-dr -di\" to disable some sample auto process (see xgm2tool to get more info)");
            Console.WriteLine();
            Console.WriteLine("Syntax for multi tracks XGM2 - allow PCM sharing:");
            Console.WriteLine("XGM2 name file1 [file2] [...] [options]");
            Console.WriteLine("  name      XGM2 music variable name");
            Console.WriteLine("  file(s)   path(s) of the .vgm music file(s) to convert to compiled XGM2 file");
            Console.WriteLine("                ex1: \"music.vgm\"");
            Console.WriteLine("                ex2: \"music1.vgm\" \"music2.vgm\" ...");
            Console.WriteLine("                ex3: \"*.vgm\"");
            Console.WriteLine("  options   optional(s) parameter(s) for xgm2Tool");
            Console.WriteLine("                Should always start with '-' character otherwise it won't be recognized");
            Console.WriteLine("                ex: \"-dr -di\" to disable some sample auto process (see xgm2tool to get more info)");
            return null;
        }

        string id = fields[1];
        var fileIns = new List<string>();
        string options = "";
        int f = 2;
        while (f < fields.Length)
        {
            string field = fields[f++];
            if (f == fields.Length && field.StartsWith('-')) options = field;
            else fileIns.Add(JavaCompat.AdjustPath(Compiler.ResDir, field));
        }

        if (!ExternalTools.Xgm2tool(fileIns, "out.xgc", options))
            throw new IOException("Error while compiling file(s) '" + string.Join(" ", fileIns) + "' to BIN format");

        byte[] data = File.Exists("out.xgc") ? File.ReadAllBytes("out.xgc") : null;
        try { File.Delete("out.xgc"); } catch (IOException) { }
        if (data == null)
            throw new IOException("Can't read data from result file !");

        foreach (var fileIn in fileIns)
            Compiler.AddResourceFile(fileIn);
        return new Bin(id, data, 256, 256, 0, Compression.NONE, true);
    }
}

/// <summary>Java 版と同じように、IzuSDK の bin にある xgmtool / xgm2tool.jar を呼ぶ。</summary>
static class ExternalTools
{
    /// <summary>IzuSDK の bin フォルダ: 環境変数 ISDK_BIN (旧名 SGDK_BIN)、なければ GDK / ISDK / SGDK の下の bin、なければこの dll の場所。</summary>
    public static string ToolDir
    {
        get
        {
            foreach (var name in new[] { "ISDK_BIN", "SGDK_BIN" })
            {
                var bin = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(bin)) return bin;
            }
            foreach (var name in new[] { "GDK", "ISDK", "SGDK" })
            {
                var gdk = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(gdk) && Directory.Exists(Path.Combine(gdk, "bin")))
                    return Path.Combine(gdk, "bin");
            }
            return Compiler.CurrentDir;
        }
    }

    public static bool Xgmtool(string fin, string fout, int timing, string options)
    {
        try { File.Delete(fout); } catch (IOException) { }

        string tool = Path.Combine(ToolDir, "xgmtool");
        if (OperatingSystem.IsWindows() && !File.Exists(tool) && File.Exists(tool + ".exe"))
            tool += ".exe";
        var args = new List<string> { fin, fout, "-s", timing == 0 ? "-n" : (timing == 1 ? "-p" : ""), options ?? "" };

        string cmdLine = (tool + " " + string.Join(" ", args)).TrimEnd(' ');
        Console.WriteLine("Executing " + cmdLine);
        Run(tool, args);
        return File.Exists(fout);
    }

    /// <summary>
    /// C# 版 xgm2tool (tools/xgm2tool-cs) の dll。環境変数 ISDK_XGM2TOOL (旧名 SGDK_XGM2TOOL) で指す。
    /// ないときや、C# 版が対応していない指定 (-ac など。終了コード 3) のときは xgm2tool.jar を使う。
    /// </summary>
    static string Xgm2toolCs
    {
        get
        {
            var dll = Environment.GetEnvironmentVariable("ISDK_XGM2TOOL");
            if (string.IsNullOrEmpty(dll)) dll = Environment.GetEnvironmentVariable("SGDK_XGM2TOOL");
            return !string.IsNullOrEmpty(dll) && File.Exists(dll) ? dll : null;
        }
    }

    public static bool Xgm2tool(List<string> fins, string fout, string options)
    {
        try { File.Delete(fout); } catch (IOException) { }

        var cs = Xgm2toolCs;
        if (cs != null)
        {
            var csArgs = new List<string> { cs };
            csArgs.AddRange(fins);
            csArgs.Add(fout);
            if (!string.IsNullOrEmpty(options)) csArgs.Add(options);
            csArgs.Add("-s");

            Console.WriteLine("Executing " + ("dotnet " + string.Join(" ", csArgs)).Trim());
            int code = Run("dotnet", csArgs);
            if (code != ExitNotPorted)
                return File.Exists(fout);
            Console.WriteLine("xgm2tool (C#) does not support these options, using xgm2tool.jar");
            try { File.Delete(fout); } catch (IOException) { }
        }

        var args = new List<string> { "-jar", Path.Combine(ToolDir, "xgm2tool.jar") };
        args.AddRange(fins);
        args.Add(fout);
        if (!string.IsNullOrEmpty(options)) args.Add(options);
        args.Add("-s");

        Console.WriteLine("Executing " + ("java " + string.Join(" ", args)).Trim());
        Run("java", args);
        return File.Exists(fout);
    }

    const int ExitNotPorted = 3;

    static int Run(string exe, List<string> args)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p == null) return -1;
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            Console.Error.WriteLine("Can't run '" + exe + "': " + e.Message);
            return -1;
        }
    }
}
