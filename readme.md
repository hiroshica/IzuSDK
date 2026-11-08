# IsuSDK（ISDK）

レトロゲーム開発のための統合環境 **IsuSDK**（略称 **ISDK**）です。
今はメガドライブ用で、開発キット [SGDK](https://github.com/Stephane-D/SGDK)（Stephane Dallongeville 作、2.11 がもと）から派生し、
自前の C# 製コンパイラ **SurugaCC** でビルドするように作り直しています。
本家からは切り離してあり、本家の更新は取り込みません。

SGDK との互換のため、関数名（`SPR_*`, `VDP_*` など）やマクロ（`SGDK`, `SGDK_VERSION`）、環境変数 `GDK` はそのまま使えます。
IsuSDK としての版数は `ISDK_VERSION`、目印のマクロは `ISDK` です（`genesis.h`）。

## 本家 SGDK との主な違い

- ビルドは SurugaCC だけ（GCC・make・Code::Blocks・CMake は使わない）: [surugacc/README.md](surugacc/README.md)
- ライブラリのソースを HAL / ミドルウェア / Util の層に分けた（V1）: [doc/v1_layers.md](doc/v1_layers.md)
- スプライトは SPRX（拡張コマ）と JSON のスプライトリソース SPRJ が標準: [doc/sprx.md](doc/sprx.md)
- リソース変換（rescomp）と xgm2tool の C# 版: `tools/rescomp-cs`, `tools/xgm2tool-cs`

## 使い方

```
surugacc\build.cmd -LibOnly                  # ライブラリだけ
surugacc\build.cmd sample\basics\hello-world  # プロジェクトをビルド（out/rom.bin ができる）
```

要るもの（SurugaCC、PowerShell 7.4 以降、.NET 8 SDK、Java）とくわしい使い方は [surugacc/README.md](surugacc/README.md) にあります。
新しいプロジェクトは `project/template` を写して始めます。

## フォルダ

| フォルダ | 中身 |
|---|---|
| `inc/`, `src/`, `res/` | ライブラリのヘッダー、ソース、ライブラリのリソース |
| `surugacc/` | SurugaCC でビルドするスクリプト |
| `tools/` | C# 版の rescomp / xgm2tool と sjasm（Z80 アセンブラ）のソース |
| `bin/` | ビルドで使う道具（rescomp.jar, xgmtool.exe と libgcc_s_dw2-1.dll, xgm2tool.jar, apj.jar, lz4w.jar, sjasm.exe） |
| `doc/` | IsuSDK の説明 |
| `project/template/` | 新しいプロジェクトのひな形 |
| `sample/` | サンプル（本家のものと IsuSDK で足したもの） |

## ライセンス

IsuSDK は SGDK から派生したもので、ライブラリと道具は SGDK と同じ MIT ライセンスです（[license.txt](license.txt)、Copyright Stephane Dallongeville）。
`bin/libgcc_s_dw2-1.dll`（xgmtool.exe が使う）は GCC の実行時ライブラリ例外つきの GPL です（[COPYING.RUNTIME](COPYING.RUNTIME)）。
