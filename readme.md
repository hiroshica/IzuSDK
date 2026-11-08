# IzuSDK（ISDK）

レトロゲーム開発のための統合環境 **IzuSDK**（略称 **ISDK**）です。
今はメガドライブ用で、開発キット [SGDK](https://github.com/Stephane-D/SGDK)（Stephane Dallongeville 作、2.11 がもと）から派生し、
自前の C# 製コンパイラ **SurugaCC** でビルドするように作り直しています。
本家からは切り離してあり、本家の更新は取り込みません。

リポジトリ: https://github.com/hiroshica/IzuSDK （旧名 hiroshica/SGDK）

SGDK との互換のため、関数名（`SPR_*`, `VDP_*` など）やマクロ（`SGDK`, `SGDK_VERSION`）、環境変数 `GDK` はそのまま使えます。
IzuSDK としての版数は `ISDK_VERSION`、目印のマクロは `ISDK` です（`genesis.h`）。

## 本家 SGDK との主な違い

- ビルドは SurugaCC だけ（GCC・make・Code::Blocks・CMake は使わない）: [surugacc/README.md](surugacc/README.md)
- ライブラリのソースを HAL / ミドルウェア / Util の層に分けた（V1）: [doc/v1_layers.md](doc/v1_layers.md)
- スプライトは SPRX（拡張コマ）と JSON のスプライトリソース SPRJ が標準: [doc/sprx.md](doc/sprx.md)
- リソース変換（rescomp）の C# 版: `tools/rescomp-cs`
- 音のドライバは MDSDRV だけ（XGM / XGM2 は外した）: [doc/mdsdrv.md](doc/mdsdrv.md)

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
| `tools/` | C# 版の rescomp と sjasm（Z80 アセンブラ）のソース |
| `bin/` | ビルドで使う道具（rescomp.jar, apj.jar, lz4w.jar, sjasm.exe） |
| `doc/` | IzuSDK の説明 |
| `project/template/` | 新しいプロジェクトのひな形 |
| `sample/` | サンプル（本家のものと IzuSDK で足したもの） |

## ライセンス

IzuSDK は SGDK から派生したもので、ライブラリと道具は SGDK と同じ MIT ライセンスです（[license.txt](license.txt)、Copyright Stephane Dallongeville）。
MDSDRV のラッパー（`src/mw/snd/mdsdrv.c`, `inc/snd/mdsdrv.h`）とドライバ本体（`res/sound/mdsdrv.bin`）は Ian Karlsson 作の MDSDRV で、zlib ライセンスです（ファイルの先頭に書いてあります）。
