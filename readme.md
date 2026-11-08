# SGDK（SurugaCC 版フォーク）

メガドライブ用の開発キット [SGDK](https://github.com/Stephane-D/SGDK)（Stephane Dallongeville 作、2.11 がもと）を、
自前の C# 製コンパイラ **SurugaCC** でビルドするように作り直しているフォークです。
本家からは切り離してあり、本家の更新は取り込みません。

## 本家との主な違い

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
| `bin/` | ビルドで使う道具（rescomp.jar, xgmtool, xgm2tool.jar, apj.jar, lz4w.jar, sjasm.exe） |
| `doc/` | このフォークの説明 |
| `project/template/` | 新しいプロジェクトのひな形 |
| `sample/` | サンプル（本家のものとこのフォークで足したもの） |

## ライセンス

SGDK のライブラリと道具は MIT ライセンスです（[license.txt](license.txt)）。
