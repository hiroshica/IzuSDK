using Sgdk.Rescomp;

// rescomp.jar (Launcher.java) と同じ引数
string fileName = null, fileNameOut = null, depTarget = null;
bool asm = true, header = true, dep = false;

foreach (var param in args)
{
    if (param.Equals("-noasm", StringComparison.OrdinalIgnoreCase)) asm = false;
    else if (param.Equals("-noheader", StringComparison.OrdinalIgnoreCase)) header = false;
    else if (param.Equals("-dep", StringComparison.OrdinalIgnoreCase)) dep = true;
    else if (fileName == null) fileName = param;
    else if (fileNameOut == null) fileNameOut = param;
    else if (dep && depTarget == null) depTarget = param;
}

Console.WriteLine("ResComp 4.02 (C#) - SGDK Resource Compiler - Copyright 2026 (Stephane Dallongeville)");

if (fileName == null)
{
    Console.WriteLine("Error: missing the input file.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  rescomp input [output] [-noasm] [-noheader] [-dep <target_file>]");
    Console.WriteLine("    input: the input resource file (.res)");
    Console.WriteLine("    output: the asm output filename (same name is used for the include file)");
    Console.WriteLine("    -noasm: specify that we don't want to generate the assembly file (.s)");
    Console.WriteLine("    -noheader: specify that we don't want to generate the header file (.h)");
    Console.WriteLine("    -dep: generate dependencies file (.d) for makefile");
    Console.WriteLine("      <target_file> allow to specify the target filename in the .d file (not the destination of the .d file itself)");
    Console.WriteLine("  Ex: rescomp resources.res outres.s");
    Console.WriteLine("  Ex: rescomp resources.res outres.s -noheader -dep");
    Console.WriteLine("  Ex: rescomp resources.res outres.s -dep out/res/gfx.o");
    return 1;
}

Console.WriteLine();

fileNameOut ??= JavaCompat.SetExtension(fileName, ".s");
if (dep && depTarget == null)
    depTarget = fileNameOut;

if (Compiler.Compile(fileName, fileNameOut, asm, header, depTarget))
    return 0;
// 3: まだ C# に移していない種類があった (surugacc/build.ps1 はこのとき rescomp.jar で作り直す)
return Compiler.UnsupportedType ? 3 : 255;
