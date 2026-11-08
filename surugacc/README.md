# surugacc/ — SurugaCC でこの SGDK をビルドする

このフォークのビルドは、自前の C# 製コンパイラ SurugaCC で行います (GCC・make は使いません)。
このフォルダはそのための入口です。本家 SGDK の更新はもう取り込まないので、本家のファイルとの差は気にせず直してかまいません。

## 要るもの

- SurugaCC (`surugacc.exe`)。環境変数 `SURUGACC` か `SURUGACC_HOME` を設定するか、PATH に置く
  (SurugaCC の `setup.ps1 -Persist` で設定できる)
- PowerShell 7.4 以降 (`pwsh`)
- Java (今は rescomp.jar を使うため。C# 版に置き換えたら要らなくなる。sizebnd.jar はもう使わない)
- sjasm (Windows では `bin/sjasm.exe` をそのまま使う)

## 始め方 (SurugaCC の setup.bat から)

```
C:\Users\hiro_\working\tools\SurugaCC\setup.bat <このリポジトリのフォルダ>
```

setup.bat が PATH・`SURUGACC`・`GDK` を設定するので、そのウィンドウで下の使い方のとおりにビルドできます
(`/persist` を付ければ次からのウィンドウでも)。

## 使い方

```
surugacc\build.cmd sample\basics\hello-world      # Windows (cmd)
pwsh surugacc/build.ps1 sample/basics/hello-world  # PowerShell
surugacc/build.sh sample/basics/hello-world        # Linux / macOS
pwsh surugacc/build.ps1 -g C:\work\mygame          # デバッグ情報つき
pwsh surugacc/build.ps1 -LibOnly                   # ライブラリだけ
```

- SGDK は既定でこのリポジトリを使う (`-Sgdk` で別の SGDK も指定できる)。
- できるもの: プロジェクトの `out/rom.bin` と `out/rom.out` (ELF)。
- ライブラリの .o は `surugacc/out/lib-O2` などに置く。

SurugaCC の `tools/sgdk.ps1` をもとに、このリポジトリ用に絞ったものです。

## ライブラリの構成 (V1)

ライブラリのソースは層ごとに `src/hal` / `src/mw` / `src/util` に分けてあります (`doc/v1_layers.md`)。
ヘッダーを変えるとライブラリを全部作り直し、移動前のソースの古い .o は自動で消します。

## これからの予定

1. SurugaCC でビルドできるようにする (このフォルダ)
2. sizebnd.jar をやめる (済み。同じ計算を build.ps1 の中で行う)
3. rescomp を C# に移す (拡張用のコンバーターを組み込めるようにする)
4. スプライト機能を拡充する

