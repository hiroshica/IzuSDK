# surugacc/ — SurugaCC でこの SGDK をビルドする

このフォルダは、本家 SGDK のファイルを変えずに、自前の C# 製コンパイラ SurugaCC でビルドするための追加分です。
本家の更新を取り込みやすくするため、このフォークで足すものはできるだけこのフォルダ (と、今後足す新しいフォルダ) に置きます。

## 要るもの

- SurugaCC (`surugacc.exe`)。環境変数 `SURUGACC` か `SURUGACC_HOME` を設定するか、PATH に置く
  (SurugaCC の `setup.ps1 -Persist` で設定できる)
- PowerShell 7.4 以降 (`pwsh`)
- Java (今は rescomp.jar と sizebnd.jar を使うため。C# 版に置き換えたら要らなくなる)
- sjasm (Windows では `bin/sjasm.exe` をそのまま使う)

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
- ライブラリの .o は `surugacc/out/lib-O2` などに置く (本家の `lib/` には何も作らない)。

SurugaCC の `tools/sgdk.ps1` をもとに、このリポジトリ (本家 master 相当) 用に絞ったものです。

## 本家の更新を取り込む

```
git remote add upstream https://github.com/Stephane-D/SGDK.git   # 最初の一回だけ
git fetch upstream
git merge upstream/master
```

本家のファイルを変えていなければ、ぶつかるのはこのフォルダの外で足したものだけです。

## これからの予定

1. SurugaCC でビルドできるようにする (このフォルダ)
2. sizebnd.jar をやめ、`surugacc link --rom-pad --rom-checksum` で済ませる
3. rescomp を C# に移す (拡張用のコンバーターを組み込めるようにする)
4. スプライト機能を拡充する

