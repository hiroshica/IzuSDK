# rescomp (C# 版)

`tools/rescomp`（Java 版 rescomp.jar）を C# (.NET 8) に移植したものです。
出力（`.s` / `.h` / `.d`）が rescomp.jar とバイト単位で同じになることを目標にしています。

## ビルドと実行

```
dotnet build tools/rescomp-cs/src/RescompCs.csproj -c Release -o surugacc/out/rescomp
dotnet surugacc/out/rescomp/rescomp.dll <input.res> [output.s] [-noasm] [-noheader] [-dep <target>]
```

`surugacc/build.ps1` は dotnet があれば自動でビルドして C# 版を使います。
環境変数 `ISDK_RESCOMP=java`（旧名 `SGDK_RESCOMP` も可）を設定すると、これまでどおり rescomp.jar を使います。

## 移植の状況

| 種類 | 状況 |
|---|---|
| ALIGN / UNGROUP / NEAR | 済み |
| BIN（NONE / APLIB / LZ4W / FAST / AUTO） | 済み |
| PALETTE（PNG / BMP / PAL / TSX） | 済み |
| BITMAP / TILESET / TILEMAP / IMAGE | 済み（TSX / TMX も） |
| SPRITE | 済み（SLOW / MAX の最適化は 1 スレッドで回すので jar より遅い） |
| MAP | 済み（画像と TMX の両方） |
| OBJECTS | 済み（オブジェクト間の参照の解決も） |
| WAV | 済み（PCM の WAV。Java Sound の変換・再サンプリングを同じ結果になるように移した。float などの WAV は jar に任せる） |
| XGM | 済み（Java 版と同じく IsuSDK の bin にある xgmtool を呼ぶ） |
| XGM2 | 済み（`tools/xgm2tool-cs` の C# 版 xgm2tool を呼ぶ。環境変数 `ISDK_XGM2TOOL`（旧名 `SGDK_XGM2TOOL`）にその dll を指すと使い、なければ・対応していない指定のときは `java -jar bin/xgm2tool.jar`） |
| SPRJ / SPRJ_KINDS | このフォーク独自（JSON スプライト → 拡張スプライトエンジン用のデータ。`doc/sprx.md` を参照） |

C# 版がまだ扱えないもの（PCM 以外の WAV など）が `.res` にあると、終了コード 3 で終わります。
`build.ps1` はそのとき rescomp.jar で作り直します。

xgmtool / xgm2tool.jar は環境変数 `ISDK_BIN`（旧名 `SGDK_BIN`）のフォルダから探します（なければ `GDK` / `ISDK` / `SGDK` の下の `bin`）。
`build.ps1` と `compare.ps1` は自分で `ISDK_BIN` を設定します。

## rescomp.jar と比べる

```
pwsh tools/rescomp-cs/compare.ps1 <.res ファイルかフォルダ> ...
```

C# 版と rescomp.jar の両方で変換して、`.s` / `.h` / `.d` が同じかどうかを表示します。
どちらもエラーで止まったものは「どちらもエラー」として別に数えます。

## 拡張（独自コンバーター）

`.res` と同じフォルダにある `*_ext.dll` を読み込み、`Isdk.Rescomp.IProcessor` を実装した型を登録します
（Java 版の `*_ext.jar` と同じ仕組みです）。

```csharp
using Isdk.Rescomp;

public class MyProcessor : IProcessor
{
    public string Id => "MYTYPE";
    public Resource Execute(string[] fields) { /* fields[0] は "MYTYPE" */ ... }
}
```

拡張 dll は `rescomp.dll` を参照してビルドしてください。
