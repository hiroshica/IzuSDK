# xgm2tool-cs

`bin/xgm2tool.jar` (Java) を C# (.NET 8) に移したものです。java が要らなくなります。
元の場所 (`tools/xgm2tool`) は変えていません。

## 使い方

    dotnet build -c Release src -o out
    dotnet out/xgm2tool.dll in.vgm out.xgc [-di] [-dr] [-dd] [-n] [-p] [-s] [-v]
    dotnet out/xgm2tool.dll a.vgm b.vgm c.vgm out.xgc      (複数曲)

`surugacc/build.ps1` と `tools/rescomp-cs/compare.ps1` は、これを自動でビルドして
環境変数 `ISDK_XGM2TOOL`（旧名 `SGDK_XGM2TOOL`）に dll の場所を入れ、rescomp (C#) の XGM2 から呼びます。

## Java 版との違い

- 出力 (.xgc / .xgm) は Java 版とバイト単位で同じになるように作ってあります
  (サンプルの VGM 全部、単曲・複数曲、`-di -dr -dd -n -p` で確かめ済み)。
- 画面 (GUI) と、音の指紋で比べる `-ac` は移していません。これらを指定すると終了コード 3 で終わるので、
  rescomp (C#) は自動で `xgm2tool.jar` に切り替えます。
- Java の動きに合わせた部品は `src/J.cs` と `src/Util.cs`、`src/Sound/JavaSound.cs` にあります
  (符号付きの byte、HashMap の並び順、安定ソート、Java Sound のリサンプル)。
