<#
.SYNOPSIS
    C# 版の rescomp と rescomp.jar に同じ .res を通して、出力 (.s / .h / .d) が同じかを比べる。

.EXAMPLE
    pwsh tools/rescomp-cs/compare.ps1 sample/basics/image/res/resources.res
.EXAMPLE
    pwsh tools/rescomp-cs/compare.ps1 (Get-ChildItem sample -Recurse -Filter *.res)
#>
param(
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments)][string[]]$Res
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$src = Join-Path $PSScriptRoot 'src'
$outDir = Join-Path $PSScriptRoot 'bin/compare'
$jar = Join-Path $root 'bin/rescomp.jar'

& dotnet build (Join-Path $src 'RescompCs.csproj') -c Release -o $outDir --nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'compare.ps1: C# の rescomp をビルドできませんでした' }
$dll = Join-Path $outDir 'rescomp.dll'
# xgmtool / xgm2tool.jar は IzuSDK の bin にある (Java 版は rescomp.jar の隣を見る)
$env:ISDK_BIN = Join-Path $root 'bin'
# XGM2 は C# 版 xgm2tool と同じ出力になるかも一緒に確かめる (jar 側の rescomp は xgm2tool.jar のまま)
$x2Out = Join-Path $PSScriptRoot 'bin/compare-xgm2tool'
& dotnet build (Join-Path $root 'tools/xgm2tool-cs/src/Xgm2Tool.csproj') -c Release -o $x2Out --nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'compare.ps1: C# の xgm2tool をビルドできませんでした' }
$env:ISDK_XGM2TOOL = Join-Path $x2Out 'xgm2tool.dll'

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('rescomp-compare-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$same = 0; $diff = 0; $skip = 0; $fail = 0
try {
    foreach ($r in $Res) {
        $file = (Resolve-Path $r).Path
        $name = [IO.Path]::GetFileNameWithoutExtension($file)
        $j = Join-Path $tmp "j/$name"; $c = Join-Path $tmp "c/$name"
        New-Item -ItemType Directory -Force $j, $c | Out-Null
        Push-Location (Split-Path $file)
        try {
            $leaf = Split-Path $file -Leaf
            & dotnet $dll $leaf "$c/$name.s" -dep "out/$name.o" > "$c.log" 2>&1
            $codeC = $LASTEXITCODE
            if ($codeC -eq 3) { Write-Host "skip  $r (まだ C# に移していない種類がある)"; $skip++; continue }
            & java -jar $jar $leaf "$j/$name.s" -dep "out/$name.o" > "$j.log" 2>&1
            $codeJ = $LASTEXITCODE
        } finally { Pop-Location }

        if ($codeC -ne 0 -and $codeJ -ne 0) {
            Write-Host "fail  $r (どちらもエラー: C# $codeC / Java $codeJ)" -ForegroundColor Yellow
            $fail++
            continue
        }
        $bad = @()
        if (($codeC -eq 0) -ne ($codeJ -eq 0)) { $bad += "終了コード (C# $codeC / Java $codeJ)" }
        foreach ($e in 's', 'h', 'd') {
            $fj = "$j/$name.$e"; $fc = "$c/$name.$e"
            $hj = Test-Path $fj; $hc = Test-Path $fc
            if ($hj -ne $hc) { $bad += ".$e (片方にしかない)"; continue }
            if (-not $hj) { continue }
            if ((Get-FileHash $fj).Hash -ne (Get-FileHash $fc).Hash) { $bad += ".$e" }
        }
        if ($bad) {
            Write-Host "DIFF  $r : $($bad -join ', ')" -ForegroundColor Red
            Write-Host "      Java: $j  C#: $c"
            $diff++
        } else {
            Write-Host "same  $r"
            $same++
        }
    }
} finally {
    if ($diff -eq 0) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
}
Write-Host "同じ $same / 違う $diff / どちらもエラー $fail / 飛ばした $skip"
if ($diff -gt 0) { exit 1 }
