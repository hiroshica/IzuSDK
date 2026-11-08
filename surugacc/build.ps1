<#
.SYNOPSIS
    IzuSDK (ISDK。SGDK から派生) のプロジェクトを SurugaCC でメガドライブの ROM にする。

.DESCRIPTION
    SurugaCC の tools/sgdk.ps1 をもとにした、このリポジトリ用の入口 (IzuSDK のビルドはこれだけ)。
    本家 SGDK の makefile.gen / makelib.gen と同じ手順を surugacc で行う。

    - IzuSDK は既定でこのスクリプトの一つ上のフォルダ (このリポジトリ) を使う。
    - リソースの変換は C# 版の rescomp (tools/rescomp-cs。dotnet があれば surugacc/out/rescomp に自動でビルドする) を使う。
      まだ C# に移していない種類のリソースがあるときや dotnet がないときは bin/rescomp.jar を使う (java が必要)。
      環境変数 ISDK_RESCOMP=java (旧名 SGDK_RESCOMP も可) で、いつも rescomp.jar を使う。
    - ROM の大きさとチェックサムの調整 (sizebnd.jar と同じ計算) はこのスクリプトの中で行う。
    - Z80 のドライバ (*.s80) は sjasm でアセンブルする (Windows では bin/sjasm.exe、それ以外では PATH の sjasm)。

    できるもの: プロジェクトの out/rom.bin (ROM) と out/rom.out (ELF)。ライブラリの .o は surugacc/out/lib-O2 などに置き
    (out/ は .gitignore で無視される)、
    --gc-sections で使うものだけをリンクする。

    surugacc の探し方 (上から順に):
      環境変数 SURUGACC (コマンド。例: C:\tools\SurugaCC\surugacc.exe や "dotnet ...\surugacc.dll")
      環境変数 SURUGACC_HOME の surugacc.exe / surugacc
      PATH の surugacc

.PARAMETER Project
    プロジェクトのフォルダ。省くと今のフォルダ。

.PARAMETER Isdk
    IzuSDK のフォルダ。省くとこのリポジトリ。旧名 -Sgdk も使える。

.PARAMETER LibOnly
    IzuSDK のライブラリだけを作る。

.PARAMETER g
    デバッグ情報を付ける (out/rom.out を gdb-multiarch で読める)。

.PARAMETER Opt
    最適化のレベル (既定は 2)。

.PARAMETER j
    ライブラリを j 個ずつ並べてコンパイルする (既定は CPU の数)。

.PARAMETER IsdkLib
    ライブラリの .o を置くフォルダ (既定は surugacc/out/lib-O2 など。-g なら surugacc/out/lib-debug-O2)。
    環境変数 ISDK_LIB (旧名 SGDK_LIB) でも指定できる。旧名 -SgdkLib も使える。

.EXAMPLE
    pwsh surugacc/build.ps1 sample/basics/hello-world
.EXAMPLE
    pwsh surugacc/build.ps1 -g C:\work\mygame
.EXAMPLE
    pwsh surugacc/build.ps1 -LibOnly
#>
param(
    [Parameter(Position = 0)][string]$Project = '.',
    [Alias('Sgdk')][string]$Isdk,
    [switch]$g,
    [switch]$LibOnly,
    [Alias('O')][int]$Opt = 2,
    [int]$j = [Environment]::ProcessorCount,
    [Alias('SgdkLib')][string]$IsdkLib = $(if ($env:ISDK_LIB) { $env:ISDK_LIB } else { $env:SGDK_LIB })
)
$ErrorActionPreference = 'Stop'

# ---- surugacc を探す
function Split-Words([string]$s) {
    [regex]::Matches($s, '"([^"]*)"|(\S+)') | ForEach-Object { if ($_.Groups[1].Success) { $_.Groups[1].Value } else { $_.Groups[2].Value } }
}
function Set-SurugaCC {
    $cmd = $env:SURUGACC
    if ([string]::IsNullOrWhiteSpace($cmd) -and $env:SURUGACC_HOME) {
        foreach ($n in 'surugacc.exe', 'surugacc', 'surugacc.dll') {
            $p = Join-Path $env:SURUGACC_HOME $n
            if (Test-Path -LiteralPath $p -PathType Leaf) { $cmd = $p; break }
        }
    }
    if ([string]::IsNullOrWhiteSpace($cmd)) {
        $c = Get-Command surugacc -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($c) { $cmd = $c.Source }
    }
    if ([string]::IsNullOrWhiteSpace($cmd)) {
        throw 'build.ps1: surugacc が見つかりません。環境変数 SURUGACC か SURUGACC_HOME を設定するか (SurugaCC の setup.ps1 で設定できます)、surugacc を PATH に置いてください'
    }
    $cmd = $cmd.Trim()
    if (Test-Path -LiteralPath $cmd -PathType Leaf) {
        $script:CcExe = (Resolve-Path -LiteralPath $cmd).Path
        $script:CcPre = @()
        if ($script:CcExe -match '\.dll$') { $script:CcPre = @($script:CcExe); $script:CcExe = 'dotnet' }
        return
    }
    $words = @(Split-Words $cmd)
    $script:CcExe = $words[0]
    $script:CcPre = @($words | Select-Object -Skip 1)
}
function cc {
    & $script:CcExe @script:CcPre @args
    if ($LASTEXITCODE -ne 0) { throw "build.ps1: surugacc $($args -join ' ') が失敗しました (終了コード $LASTEXITCODE)" }
}
function java-jar {
    & java -jar @args
    if ($LASTEXITCODE -ne 0) { throw "build.ps1: java -jar $($args -join ' ') が失敗しました (終了コード $LASTEXITCODE)" }
}
Set-SurugaCC

if (-not $Isdk) { $Isdk = Split-Path -Parent $PSScriptRoot }
$gdk = (Resolve-Path $Isdk).Path
# SurugaCC の setup.bat で設定した GDK (IzuSDK の場所) が、このリポジトリと違うときは知らせる (使うのはこのリポジトリ)
$gdkEnv = if ($env:GDK) { $env:GDK } elseif ($env:ISDK) { $env:ISDK } else { $env:SGDK }
if ($gdkEnv -and (Test-Path -LiteralPath $gdkEnv) -and (Resolve-Path -LiteralPath $gdkEnv).Path.TrimEnd('\', '/') -ne $gdk.TrimEnd('\', '/')) {
    Write-Warning "環境変数 GDK ($gdkEnv) はこのリポジトリ ($gdk) と違います。ここでは $gdk を使います (setup.bat <このリポジトリ> で GDK を合わせられます)"
}
$proj = (Resolve-Path $Project).Path
$debug = @(if ($g) { '-g' })
$lib = if ($IsdkLib) { $IsdkLib } else { Join-Path $PSScriptRoot "out/lib$(if ($g) { '-debug' })$(if ($Opt -gt 0) { "-O$Opt" })" }
if (-not (Test-Path "$gdk/bin/rescomp.jar")) {
    throw "build.ps1: $gdk/bin/rescomp.jar がありません (IzuSDK のフォルダを確かめてください)"
}

# 本家 SGDK の makefile.gen / makelib.gen と同じオプション (警告などのオプションは surugacc では意味がないので省く)
$CFLAGS = @('--target', 'megadrive', '-m68000', "-O$Opt", '-fno-builtin', '-fms-extensions', '-ffunction-sections', '-fdata-sections') + $debug
if ($g) { $CFLAGS += '-DDEBUG=1' }
$AFLAGS = @('--target', 'megadrive', '-m68000', '-x', 'assembler-with-cpp', '-Wa,--register-prefix-optional,--bitwise-or') + $debug

# ---- rescomp: C# 版 (tools/rescomp-cs) を使い、まだ C# に移していない種類があれば rescomp.jar で作り直す
# 環境変数 ISDK_RESCOMP=java (旧名 SGDK_RESCOMP) で、いつも rescomp.jar を使う
$script:UseJavaRescomp = ($env:ISDK_RESCOMP -eq 'java') -or ($env:SGDK_RESCOMP -eq 'java')
$script:RescompCsChecked = $false
$script:RescompCs = $null
function Get-RescompCs {
    if ($script:RescompCsChecked) { return $script:RescompCs }
    $script:RescompCsChecked = $true
    if ($script:UseJavaRescomp) { return $null }
    $src = Join-Path $gdk 'tools/rescomp-cs/src'
    if (-not (Test-Path -LiteralPath (Join-Path $src 'RescompCs.csproj'))) { return $null }
    $outDir = Join-Path $PSScriptRoot 'out/rescomp'
    $dll = Join-Path $outDir 'rescomp.dll'
    $stale = -not (Test-Path -LiteralPath $dll)
    if (-not $stale) {
        $built = (Get-Item -LiteralPath $dll).LastWriteTimeUtc
        $stale = [bool](Get-ChildItem -LiteralPath $src -Recurse -File -Include '*.cs', '*.csproj' |
            Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and $_.LastWriteTimeUtc -gt $built })
    }
    if ($stale) {
        if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
            Write-Warning 'dotnet がないので、rescomp は rescomp.jar を使います (C# 版を使うには .NET 8 SDK を入れてください)'
            return $null
        }
        Write-Host 'rescomp (C#) をビルド'
        $log = & dotnet build (Join-Path $src 'RescompCs.csproj') -c Release -o $outDir --nologo -v q 2>&1
        if ($LASTEXITCODE -ne 0) {
            $log | ForEach-Object { Write-Host "$_" }
            Write-Warning 'rescomp (C#) をビルドできなかったので、rescomp.jar を使います'
            return $null
        }
    }
    $script:RescompCs = $dll
    return $dll
}
function Invoke-Rescomp {
    $dll = Get-RescompCs
    if ($dll) {
        & dotnet $dll @args
        if ($LASTEXITCODE -eq 0) { return }
        if ($LASTEXITCODE -ne 3) { throw "build.ps1: rescomp (C#) $($args -join ' ') が失敗しました (終了コード $LASTEXITCODE)" }
        Write-Host 'まだ C# の rescomp に移していない種類があるので、rescomp.jar で作り直します'
    }
    java-jar "$gdk/bin/rescomp.jar" @args
}

# 出力が古いか、なければ $true
function Test-Stale([string]$Out, [string]$In) {
    -not (Test-Path -LiteralPath $Out) -or (Get-Item -LiteralPath $In).LastWriteTimeUtc -gt (Get-Item -LiteralPath $Out).LastWriteTimeUtc
}

# 文字コードの順に並べる (sh の sort と同じ順にして、リンクする順番を変えないため)
function Sort-Ordinal {
    $a = [string[]]@($input)
    [Array]::Sort($a, [StringComparer]::Ordinal)
    $a
}

# ディレクトリの下のファイルを、そのディレクトリからの相対パス (区切りは /) で並べる
function Get-RelativeFiles([string]$Dir, [string]$Sub, [string]$Filter, [string[]]$Exclude = @()) {
    if (-not (Test-Path (Join-Path $Dir $Sub))) { return @() }
    Get-ChildItem (Join-Path $Dir $Sub) -Recurse -File -Filter $Filter |
        ForEach-Object { [IO.Path]::GetRelativePath($Dir, $_.FullName).Replace('\', '/') } |
        Where-Object { $f = $_; -not ($Exclude | Where-Object { $f.StartsWith($_) }) } |
        Sort-Ordinal
}

# Z80 のアセンブラ sjasm (本家 SGDK の makelib.gen と同じもの)
function Get-Sjasm {
    if ($IsWindows -and (Test-Path -LiteralPath "$gdk/bin/sjasm.exe")) { return "$gdk/bin/sjasm.exe" }
    $c = Get-Command sjasm -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($c) { return $c.Source }
    throw ('build.ps1: Z80 のアセンブラ sjasm が見つかりません。Windows では <IzuSDK>/bin/sjasm.exe を使います。' +
           'Linux・macOS では tools/sjasm/src で make して、できた sjasm を PATH に置いてください')
}

# 本家 SGDK の bintos と同じ: バイナリ $Bin から、$OutBase.s (.rodata の dc.w の並び) と $OutBase.h (extern const u8 名前[大きさ]) を作る
function Convert-BinToS([string]$Bin, [string]$OutBase) {
    # [IO.File] は PowerShell の今のフォルダ (Push-Location) ではなくプロセスのフォルダを基準にするので、フルパスにする
    $Bin = [IO.Path]::GetFullPath($Bin, (Get-Location).Path)
    $OutBase = [IO.Path]::GetFullPath($OutBase, (Get-Location).Path)
    $data = [IO.File]::ReadAllBytes($Bin)
    $name = [IO.Path]::GetFileName($OutBase)
    $sb = [Text.StringBuilder]::new()
    [void]$sb.Append(".section .rodata`n`n    .align  2`n`n    .global $name`n${name}:`n")
    for ($pos = 0; $pos -lt $data.Length; $pos += 4096) {
        $len = [Math]::Min(4096, $data.Length - $pos)
        $words = for ($i = 0; $i -lt $len; $i += 2) {
            $lo = if ($i + 1 -lt $len) { $data[$pos + $i + 1] } else { 0 }
            '0x{0:X2}{1:X2}' -f $data[$pos + $i], $lo
        }
        [void]$sb.Append("    dc.w    $($words -join ',')`n")
    }
    [void]$sb.Append("`n")
    [IO.File]::WriteAllText("$OutBase.s", $sb.ToString())
    $guard = $name.ToUpperInvariant()
    [IO.File]::WriteAllText("$OutBase.h", "#ifndef _${guard}_H_`n#define _${guard}_H_`n`nextern const u8 $name[0x$('{0:X}' -f $data.Length)];`n`n#endif // _${guard}_H_`n")
}

# .s80 (Z80 のアセンブリ) を sjasm でアセンブルし、bintos と同じ .s / .h を $OutBase.s / .h に作る
function Build-S80([string]$Src, [string]$OutBase, [string[]]$IncDirs) {
    New-Item -ItemType Directory -Force (Split-Path $OutBase) | Out-Null
    $sj = Get-Sjasm
    # sjasm (Windows 版) は見つからないファイルを空の名前で開こうとして止まる (デバッグ版ではアサートのダイアログが出る) ので、
    # ソース・出力・インクルードのフォルダはすべて OS の区切り文字のフルパスにして渡す (存在しないフォルダは渡さない)
    $sep = [IO.Path]::DirectorySeparatorChar
    $full = { param([string]$p) [IO.Path]::GetFullPath($p, (Get-Location).Path).Replace('/', $sep).Replace('\', $sep) }
    $srcFull = & $full $Src
    $outFull = & $full $OutBase
    $incs = @($IncDirs | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | ForEach-Object { '-i' + (& $full $_).TrimEnd($sep) })
    Write-Host "sjasm $Src"
    Remove-Item -LiteralPath "$OutBase.o80" -ErrorAction SilentlyContinue
    $log = & $sj @incs $srcFull "$outFull.o80" "$outFull.lst" 2>&1
    if (-not (Test-Path -LiteralPath "$OutBase.o80")) {
        $log | ForEach-Object { Write-Host "$_" }
        throw "build.ps1: sjasm が $Src をアセンブルできませんでした ($OutBase.lst も見てください)"
    }
    Convert-BinToS "$OutBase.o80" $OutBase
}

# 本家 SGDK の sizebnd.jar (-sizealign $Align -checksum) と同じ: ROM の大きさを $Align の倍数にし (0 で埋める)、
# ヘッダの 0x18E にチェックサム (ROM 全体を 32 ビットずつ XOR し、上下 16 ビットを XOR したもの) を書く
function Set-RomSizeAndChecksum([string]$Rom, [int]$Align = 131072) {
    $Rom = [IO.Path]::GetFullPath($Rom, (Get-Location).Path)
    $data = [IO.File]::ReadAllBytes($Rom)
    $len = $data.Length
    $needed = $len -band ($Align - 1)
    if ($needed -ne 0) { $needed = $Align - $needed }
    if ($needed -ne 0) { $buf = [byte[]]::new($len + $needed); [Array]::Copy($data, $buf, $len); $data = $buf }
    $data[0x18E] = 0; $data[0x18F] = 0
    [uint32]$sum = 0
    for ($i = 0; $i -lt $data.Length; $i += 4) {
        $sum = $sum -bxor (([uint32]$data[$i] -shl 24) -bor ([uint32]$data[$i + 1] -shl 16) -bor ([uint32]$data[$i + 2] -shl 8) -bor [uint32]$data[$i + 3])
    }
    $c = ($sum -bxor ($sum -shr 16)) -band 0xFFFF
    $data[0x18E] = [byte]($c -shr 8); $data[0x18F] = [byte]($c -band 0xFF)
    [IO.File]::WriteAllBytes($Rom, $data)
}

# ---- ライブラリ
New-Item -ItemType Directory -Force $lib | Out-Null
$lib = (Resolve-Path $lib).Path
# ビルドのときに作るファイル (Z80 のドライバの .s / .h と res/libres.h)。makelib.gen の out/release に当たる
$gen = Join-Path $lib 'gen'
New-Item -ItemType Directory -Force "$gen/res" | Out-Null
$genAsm = [ordered]@{}
# res/libres.rs と res/libres.h (makelib.gen と同じく IzuSDK のフォルダで rescomp を動かす)
if (Test-Stale "$gen/res/libres.rs" "$gdk/res/libres.res") {
    Push-Location $gdk
    try { Invoke-Rescomp res/libres.res "$gen/res/libres.rs" } finally { Pop-Location }
}
$genAsm['res/libres.rs'] = "$gen/res/libres.rs"
# Z80 のドライバ: 隣にできあがりの .s があるものはそれを使い、ないものは sjasm でアセンブルする
foreach ($f in Get-RelativeFiles $gdk src '*.s80') {
    $base = $f -replace '\.s80$', ''
    if (Test-Path -LiteralPath "$gdk/$base.s") { continue }
    if (Test-Stale "$gen/$base.s" "$gdk/$f") { Build-S80 "$gdk/$f" "$gen/$base" @("$gdk/src", "$gdk/inc", "$gdk/inc/snd", $gen) }
    $genAsm["$base.s"] = "$gen/$base.s"
}
$LINC = @("-I$gdk/inc", "-I$gdk/src", "-I$gdk/res", "-I$gen")
# 1 つでもヘッダー (inc/ と src/ の .h / .i) が .o より新しければ、その .o を作り直す
# (ライブラリのソースは .h を介して互いに依存するので、ソースの日付だけでは古い .o が残る)
$newestHeader = (@(Get-ChildItem "$gdk/inc", "$gdk/src" -Recurse -File -Include '*.h', '*.i') |
                 Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
function Test-LibStale([string]$Out, [string]$In) {
    (Test-Stale $Out $In) -or ($newestHeader -and (Get-Item -LiteralPath $Out).LastWriteTimeUtc -lt $newestHeader)
}
$jobs = [Collections.Generic.List[object]]::new()
$libObjNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($f in Get-RelativeFiles $gdk src '*.c' 'src/boot/') {
    $o = Join-Path $lib (($f -replace '\.c$', '').Replace('/', '_') + '.o')
    [void]$libObjNames.Add([IO.Path]::GetFileName($o))
    if (Test-LibStale $o "$gdk/$f") { $jobs.Add(@($CFLAGS + $LINC + @('-c', "$gdk/$f", '-o', $o))) }
}
$asmSrc = [ordered]@{}
foreach ($f in Get-RelativeFiles $gdk src '*.s' 'src/boot/') { $asmSrc[$f] = "$gdk/$f" }
foreach ($k in $genAsm.Keys) { $asmSrc[$k] = $genAsm[$k] }
foreach ($f in $asmSrc.Keys) {
    $o = Join-Path $lib (($f -replace '\.[^.]*$', '').Replace('/', '_') + '.o')
    [void]$libObjNames.Add([IO.Path]::GetFileName($o))
    if (Test-LibStale $o $asmSrc[$f]) { $jobs.Add(@($AFLAGS + $LINC + @('-c', $asmSrc[$f], '-o', $o))) }
}
# ソースが移動・削除された .o (例: src/sys.c → src/mw/sys.c で残る src_sys.o) は消す。残すと二重定義でリンクできない
foreach ($o in @(Get-ChildItem $lib -File -Filter '*.o')) {
    if (-not $libObjNames.Contains($o.Name)) {
        Write-Host "古い .o を削除: $($o.Name)"
        Remove-Item -LiteralPath $o.FullName
    }
}
if ($jobs.Count -gt 0) {
    Write-Host "IzuSDK のライブラリをコンパイル ($($jobs.Count) 個) → $lib"
    $exe = $script:CcExe; $pre = $script:CcPre
    $failed = $jobs | ForEach-Object -ThrottleLimit ([Math]::Max(1, $j)) -Parallel {
        $a = $_; $exe = $using:exe; $pre = $using:pre
        & $exe @pre cc @a 2>&1 | ForEach-Object { Write-Host "$_" }
        if ($LASTEXITCODE -ne 0) { $a[-3] }
    }
    if ($failed) { throw "build.ps1: コンパイルに失敗: $($failed -join ', ')" }
}
if ($LibOnly) {
    Write-Host "IzuSDK のライブラリを作りました: $lib"
    return
}

# ---- プロジェクト
$projSources = @(Get-ChildItem -LiteralPath $proj -File -Filter '*.c') +
               @(if (Test-Path -LiteralPath (Join-Path $proj 'src')) { Get-ChildItem -LiteralPath (Join-Path $proj 'src') -Recurse -File -Filter '*.c' })
if ($projSources.Count -eq 0) {
    Write-Host "IzuSDK のライブラリを作りました: $lib"
    Write-Host "$proj には C のソース (*.c か src/*.c) がないので、プロジェクトはビルドしません。"
    Write-Host "  例: pwsh $PSCommandPath $(Join-Path $gdk 'sample/basics/hello-world')"
    return
}
Push-Location $proj
try {
    New-Item -ItemType Directory -Force out | Out-Null
    # 前のビルドとオプション (-g、-O など) が違えば、プロジェクトの .o をすべて作り直す
    $flagsFile = 'out/.surugacc-flags'
    $flags = (@($CFLAGS) + '|' + @($AFLAGS)) -join ' '
    $rebuild = -not (Test-Path -LiteralPath $flagsFile) -or (Get-Content -Raw -LiteralPath $flagsFile).Trim() -cne $flags
    if ($rebuild -and (Test-Path -LiteralPath $flagsFile)) { Write-Host 'オプションが前のビルドと違うので、プロジェクトの .o をすべて作り直します' }
    foreach ($r in Get-RelativeFiles $proj . '*.res' 'out/') {
        $rs = $r -replace '\.res$', '.rs'
        if (Test-Stale $rs $r) {
            Write-Host "rescomp $r"
            Invoke-Rescomp $r $rs -dep ('out/' + ($r -replace '\.res$', '.o'))
        }
    }
    $INC = @('-I.', '-Iinc', '-Isrc', '-Ires', '-Iout', "-I$gdk/inc", "-I$gdk/res", "-I$gen")
    # ROM のヘッダはプロジェクトの src/rom_header.c (なければ IzuSDK のものを写す)。sega.s は out/rom_header.bin を .incbin する
    New-Item -ItemType Directory -Force src | Out-Null
    if (-not (Test-Path src/rom_header.c)) { Copy-Item "$gdk/src/boot/rom_header.c" src/ }
    cc cc @CFLAGS @INC -c src/rom_header.c -o out/rom_header.o
    cc objcopy -O binary out/rom_header.o out/rom_header.bin
    $sega = if (Test-Path src/boot/sega.s) { 'src/boot/sega.s' } else { "$gdk/src/boot/sega.s" }
    cc cc @AFLAGS @INC -c $sega -o out/sega.o

    # プロジェクトの Z80 のアセンブリ (.s80) は out/ に .s と .h を作る
    $z80 = @()
    $top = @(Get-ChildItem . -File -Filter '*.s80' | ForEach-Object Name)
    foreach ($f in @($top + (Get-RelativeFiles $proj src '*.s80')) | Sort-Ordinal) {
        $base = 'out/' + ($f -replace '\.s80$', '')
        if (Test-Stale "$base.s" $f) {
            Build-S80 $f $base @('.', 'src', 'inc', 'res', 'out', "$gdk/src", "$gdk/inc", "$gdk/inc/snd", $gen)
        }
        $z80 += "$base.s"
    }

    $objs = @()
    $top = @(Get-ChildItem . -File -Filter '*.c' | ForEach-Object Name)
    foreach ($f in @($top + (Get-RelativeFiles $proj src '*.c' 'src/boot/' | Where-Object { $_ -ne 'src/rom_header.c' })) | Sort-Ordinal) {
        $o = 'out/' + ($f -replace '\.c$', '.o')
        New-Item -ItemType Directory -Force (Split-Path $o) | Out-Null
        if ($rebuild -or (Test-Stale $o $f)) { Write-Host "cc $f"; cc cc @CFLAGS @INC -c $f -o $o }
        $objs += $o
    }
    $top = @(Get-ChildItem . -File -Filter '*.s' | ForEach-Object Name)
    foreach ($f in @($top + (Get-RelativeFiles $proj src '*.s' 'src/boot/') + (Get-RelativeFiles $proj . '*.rs' 'out/') + $z80) | Sort-Ordinal) {
        $o = if ($f.StartsWith('out/')) { $f -replace '\.[^.]*$', '.o' } else { 'out/' + ($f -replace '\.[^.]*$', '.o') }
        New-Item -ItemType Directory -Force (Split-Path $o) | Out-Null
        if ($rebuild -or (Test-Stale $o $f)) { Write-Host "cc $f"; cc cc @AFLAGS @INC -c $f -o $o }
        $objs += $o
    }

    # リンク: md.ld、-nostdlib (crt0 も付けない)、libgcc に当たる関数だけ surugacc の実行時ライブラリから
    $libObjs = @(Get-ChildItem $lib -File -Filter '*.o' | ForEach-Object FullName | Sort-Ordinal)
    cc cc --target megadrive "-O$Opt" -n -T "$gdk/md.ld" -nostdlib out/sega.o @objs @libObjs -lgcc '-Wl,--gc-sections' -o out/rom.bin --elf out/rom.out
    Set-RomSizeAndChecksum (Join-Path $proj 'out/rom.bin') 131072
    Set-Content -LiteralPath $flagsFile -Value $flags -Encoding utf8
    Write-Host "$proj/out/rom.bin ($((Get-Item out/rom.bin).Length) バイト)"
} finally { Pop-Location }
