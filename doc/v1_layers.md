# V1: HAL / ミドルウェアの層に分けた SGDK（このフォーク）

計画書「SGDK 階層調査・HAL/ミドルウェア再構築 設計書」の v1（階層調査・HAL/MW 層分割・低速リスク・ASM 見積）を、このフォークに実装したときの説明です。
v2 の C++ namespace 化は含みません（下の「計画書と変えたところ」を参照）。

## 層とフォルダ

| 層 | フォルダ | 中身 |
|---|---|---|
| L0 HAL | `inc/hal/`, `src/hal/` | ハードウェアのレジスタに触る最小の層。ほとんどがマクロ |
| L1 MW（ミドルウェア） | `inc/mw/`, `src/mw/` | HAL の上の機能（VBlank スケジューラ、DMA キュー、タイル、パレット、スプライト、マップ、BMP、パッド、サウンド） |
| L2 Util | `src/util/` | ハードウェアに依らないもの（メモリ、文字列、数学、圧縮展開、プール） |
| L3 Ext | `src/ext/` | 追加モジュール（console / everdrive / fat16 / megawifi など、本家と同じ） |
| 起動 | `src/boot/` | ベクタと ROM ヘッダー（本家と同じ場所） |

公開している API のヘッダー（`vdp.h`、`sprite_eng.h` など）は、本家と同じ `inc/` に置いたままです。今までのゲームのソースは変えずにビルドできます。

### ソースの移動先

| 層 | ファイル |
|---|---|
| `src/hal/` | `hal_cpu.c`（新規）, `hal_vdp.c`（新規）, `hal_vdp_bulk_a.s`（新規）, `sys_a.s`, `error_a.s`, `kdebug.s`, `mapper.c`, `sram.c`, `sram_a.s`, `ym2612.c`, `psg.c`, `z80_ctrl.c`, `dma_a.s`, `vdp_tile_a.s` |
| `src/mw/` | `sched.c`（新規）, `sys.c`, `vdp.c`, `vdp_bg.c`, `vdp_spr.c`, `vdp_tile.c`, `dma.c`, `pal.c`, `vram.c`, `joy.c`, `timer.c`, `map.c`, `bmp.c`, `bmp_a.s`, `task.s`, `snd/`（`snd/z80_driver.c` は新規） |
| `src/mw/sprite/` | `sprite_eng.c`, `sprx.c`（以前の `src/ext/sprite_eng_ext.c`）, `sprite_port.c`（新規） |
| `src/util/` | `memory.c`, `memory_a.s`, `string.c`, `maths.c`, `maths3D.c`, `maths3D_a.s`, `tab_*.c`, `tools.c`, `tools_a.s`, `pool.c`, `object.c`, `types.c` |

## HAL（inc/hal/）

| ヘッダー | 内容 | 計画書の名前 |
|---|---|---|
| `hal_vdp.h` | VDP のポート・状態・コマンドワード（`vdp.h` から移した。名前は同じ）、`hal_vdp_write_ctrl32()` などのマクロ、レジスタの影 `halVdpRegs[]`、まとめて送る関数 `hal_vdpbulk_*()`（asm） | sgd::hal::vdp / vdp_bulk |
| `hal_cpu.h` | 割り込みのフック（`vintCB` / `hintCaller` / `eintCB` と、新しい `vintTaskCB`）、`SYS_disableInts()`、割り込みマスク | sgd::hal::cpu |
| `hal_dma.h` | DMA のレジスタ設定と開始（2 語目をメモリから書く SEGA の注意どおり）、キューを流す `flushQueue()` | sgd::hal::dma |
| `hal_z80.h` | Z80 のバス・リセット・RAM | sgd::hal::z80 |
| `hal_snd.h` | YM2612 / PSG の書き込み | sgd::hal::snd |
| `hal_io.h` | パッドのポート（データ・コントロール） | sgd::hal::io |
| `hal_mapper.h` | SSF2 のバンク、SRAM の切り替え | sgd::hal::mapper |
| `hal_timer.h` | V-Int の回数 `vtimer` と HV カウンター | sgd::hal::timer |
| `hal_debug.h` | エミュレーターのデバッグ出力（リリースでは何もしない） | sgd::hal::debug |
| `hal.h` | 上の全部を読む | |

HAL は上の層を呼びません（計画書のルール D3）。割り込みのベクター（`src/boot/sega.s`）は `vintTaskCB` と `vintCB` を呼ぶだけで、BMP の処理は `BMP_init` が `hal_cpu_set_vint_task()` で登録します。

## VBlank スケジューラ（inc/mw/sched.h）

`SYS_doVBlankProcess()` は `src/mw/sched.c` に移り、登録された処理を順に呼ぶようにしました。

```
DMA（SCHED_TASK_DMA）→ スクロール（SCHED_TASK_SCROLL）→ パレットのフェード（SCHED_TASK_PALETTE）
→ ユーザーの VBlank コールバック → CPU 負荷の表示 → パッド（SCHED_TASK_JOY）
```

- 何を登録するかは `sys.c` の `internal_reset()` の 1 か所で決めています（計画書の「束縛設定」）。
- ゲームから `SCHED_setTask(SCHED_TASK_JOY, myJoyTask)` のように差し替えられます。処理が `FALSE` を返すと、その処理の `VBlankProcess` のビットが消えます（今までと同じ）。
- `SCHED_EVENT_VRAM_LAYOUT`: VDP の面やテーブルの場所が変わったときの知らせ。スプライトエンジンが `SPR_init` で受け取りを登録します（以前は `vdp.c` が `SPR_defragVRAM()` を直接呼んでいました）。

## スプライト（新 SPR = SPRX がメイン）

- SPRX（拡張コマ、JSON の SPRJ リソース）は拡張ではなく、標準のスプライトエンジンに組み込んだメインの道にしました。`genesis.h` からいつも `sprx.h` を読みます。
- `SPR_update()` だけはポート（`inc/mw/sprite_port.h`）を通して呼び、`SPR_setEnginePort()` で実行中に別のエンジンへ差し替えられます。標準は `sprEnginePortDefault`（`SPR_updateDefault()`、SPRX 対応）です。
- `SPR_setPosition()` などの個々の関数は直接呼びます（関数ポインタにすると遅くなるため。計画書の R11）。
- 使い方は `doc/sprx.md` を見てください。

## 層違反（計画書の「層違反」シート）の対応

| # | 内容 | V1 での対応 |
|---|---|---|
| 1 | sys.c が上の層を全部 include して VBlank で直接呼ぶ | VBlank の処理はスケジューラ（`sched.c`）の登録制に。割り込みまわりは `hal_cpu.c` へ。`sys.c` は MW の起動と束縛設定の役になった |
| 2 | vdp.c がスプライトエンジンを呼ぶ | `SCHED_EVENT_VRAM_LAYOUT` で知らせる形に |
| 3 | maths.c / timer.c が vdp.h を読む | maths.c は vdp.h を読まないように。timer.c は MW なので vdp.h のまま |
| 4 | tools.c が vdp.h / mapper.h / timer.h を読む | vdp.h / timer.h をやめた。`getFPS()` / `getFPS_f()` は sys.c へ。mapper.h（`FAR_SAFE`、HAL）は残した |
| 5 | maths3D.c が bmp.h を読む | 既定の画面の大きさを `maths3D.h` の定数にし、`bmp.c` で BMP の大きさと同じか確かめる |
| 6 | z80_ctrl.c がサウンドドライバに依存 | バス・RAM は HAL（`src/hal/z80_ctrl.c`）、ドライバの読み込みとバス保護は MW（`src/mw/snd/z80_driver.c`）に分けた |
| 7 | vdp_tile.c / xgm.c がライブラリのリソースを直接読む | 未対応（次の段階） |
| 8 | dma.c が Z80 / タイマーに依存 | DMA の VBlank 処理（Z80 のバス保護つき）は MW の `DMA_doVBlankProcess()` にまとめた。レジスタ操作は HAL（`hal_dma.h`）。キューの Z80 依存は残っている |
| 9 | joy.c がポートを直接読む | ポートは `hal_io.h` のマクロ経由に |
| 10 | sega.s / sys.c が BMP / MAP / VDP スクロールを直接呼ぶ | `sega.s` は `vintTaskCB` を呼ぶだけ。`sys.c` の直接参照もなくした |

`sys.h` は `FORCE_INLINE` / `NO_INLINE` などの属性マクロも持っているので、util の一部のファイルはまだ `sys.h` を読みます（関数は呼びません。`memory.c` は HAL の `SYS_getStackPointer()` だけ）。

## 計画書と変えたところ

1. **HAL は static inline ではなくマクロ**: SurugaCC は関数のインライン展開をしません（SurugaCC の `docs/メガドライブ対応.md` の「まだできないこと」）。static inline にすると呼ぶたびに JSR/RTS が入る（計画書の R1、1 回あたり約 56 サイクル）ので、マクロにしました。どのコンパイラでも呼び出しは増えません。
2. **自動インクリメントの「同じなら書かない」はやめた**: DMA キューの項目がレジスタ 0x0F を影の表を通さずに書き換えるので、影の値と比べて省くと間違えます。`hal_vdp_set_autoinc()` はいつも書きます。
3. **C++ は使わない**: v2 は C++20（concept / constinit）が前提ですが、SurugaCC が扱えるのは C++98 のサブセット（テンプレートなし）です。V1 は C のままです。v2 をするなら、計画書の O2（C の接頭辞とマクロの別名）が SurugaCC に合います。
4. **ASM 化（A1〜A9）はしていない**: 見積もりは手計算で、計画書自身が「先に実測」と書いています。HAL のまとめて送る関数（`hal_vdpbulk_*`）だけ asm で用意しました。

## 速さ

SurugaCC（-O2）で `sample/game/sonic` を `run68k.py --profile` で 180 フレーム動かして比べると、V1 は変更前より 1 フレームあたり約 95 サイクル（1 フレームの 0.07%）多いだけでした。増えたのは `SPR_update()` のポート経由の呼び出しと、スケジューラから処理を関数ポインタで呼ぶ分です。

サンプル（hello-world, image, pools, sprites-sharing-tiles, tile-animation, cube-3D, partic, hilight-shadow, sprite-masking, sonic, console, benchmark, h-int/scaling, h-int/wobble, scroll/linescroll）と fz2 の画面は変更前と同じでした。違いは CPU 負荷を表示するカーソル（`SYS_showFrameLoad`）の位置が 1 ライン下がったことと、benchmark のメモリの数字（静的に使う RAM が 31 バイト増えた: スケジューラの表、フック、スプライトのポート）だけです。

## ビルド

- SurugaCC: `surugacc/build.ps1`（`build.cmd` / `build.sh`）。ライブラリのソースはフォルダの下を全部見るので、そのまま使えます。
  ヘッダーを変えたら全部作り直すようにし、移動前のソースの古い `.o`（`src_sys.o` など）は自動で消します。
- GCC: 使いません。`makefile.gen` / `makelib.gen` / `common.mk` / `CMakeLists.txt` / `build_*.bat`、`bin/` の GCC と make の一式、`lib/libmd*.a` / `lib/libgcc.a` は削除しました（下の「GCC で使っていたものの削除」）。

## GCC で使っていたものの削除

ビルドは SurugaCC だけにしたので、GCC のビルドにしか使わないものを消しました。

| 消したもの | 理由 |
|---|---|
| `makefile.gen`, `makelib.gen`, `common.mk` | GCC + make でのビルド手順。`surugacc/build.ps1` が同じ手順を持つ |
| `CMakeLists.txt` | m68k-elf-gcc でライブラリを作る CMake |
| `build_lib.bat`, `build_samples.bat`, `build_adv.bat` | 上の makefile を呼ぶだけ |
| `lib/libmd.a`, `lib/libmd_debug.a`, `lib/libmd_debug.dwarf4-O1.cfg`, `lib/libgcc.a` | GCC で作ったライブラリ。SurugaCC は `surugacc/out/` に自分で作り、libgcc に当たる関数は SurugaCC の実行時ライブラリを使う |
| `bin/` の `gcc.exe`, `cc1.exe`, `cpp.exe`, `as.exe`, `ld.exe`, `ar.exe`, `nm.exe`, `objcopy.exe`, `objdump.exe`, `size.exe`, `gdb.exe`, `lto1.exe`, `lto-wrapper.exe`, `liblto_plugin.dll`, `libstdc++-6.dll` | GCC のツールチェーン |
| `bin/` の `make.exe`, `sh.exe`, `cp.exe`, `mkdir.exe`, `rm.exe`, `msys-*.dll` | makefile を動かすための MSYS の道具 |
| `bin/` の `mac68k.exe`, `bintos.exe`, `convsym.exe`, `nm2wch.exe`, `sizebnd.jar` | makefile からだけ呼ぶ道具（`bintos` と `sizebnd` の計算は `build.ps1` が自分で行う。シンボルは SurugaCC の ELF を gdb で読む） |
| `tools/` の `apj`, `bintos`, `commons`, `convsym`, `lz4w`, `rescomp`（Java 版）, `sizebnd`, `xgm rom builder`, `xgm2tool`（Java 版）, `xgmtool`, `settings.gradle` | bin にある道具や jar の元のソース。使っていない（必要なら本家から取れる） |

`.vscode/tasks.json` は make を呼んでいたので、SurugaCC でライブラリを作るタスクに置き換えました。

残したもの:

- `bin/rescomp.jar`, `xgmtool.exe`, `xgm2tool.jar`, `apj.jar`, `lz4w.jar`, `sjasm.exe`: リソース変換と Z80 のアセンブル（`build.ps1` と rescomp が使う）
- `bin/libgcc_s_dw2-1.dll`: `xgmtool.exe`（XGM の変換）が使う MinGW の実行時ライブラリ。GCC を消したときにいっしょに消してしまい、XGM を使うプロジェクトの rescomp が失敗していたので戻した
- `md.ld`, `src/boot/`, `inc/asm_mac.i`: SurugaCC のリンクとアセンブルでも使う
- `tools/rescomp-cs`, `tools/xgm2tool-cs`: `build.ps1` が dotnet でビルドして使う
- `tools/sjasm`: Linux・macOS では `bin/sjasm.exe` が使えないので、ここからビルドする
- `tools/rescomp-cs/tests/sprj/run_test.sh`: PC 上でのテストに Ubuntu の `m68k-linux-gnu-gcc` と qemu を使う（リポジトリの中の GCC ではない）
- サンプルの `.cbp` などのプロジェクトファイル: 本家のサンプルのまま

## 古い痕跡の削除

このフォークは本家（Stephane-D/SGDK）から切り離し、本家の更新はもう取り込みません。サンプル（`sample/`）以外で、SurugaCC と SPRX / SPRJ だけの構成で使わない古いものを消しました。

| 消したもの | 理由 |
|---|---|
| `.github/`（`FUNDING.yml`, `ISSUE_TEMPLATE/`） | 本家のスポンサーと issue のひな形 |
| `changelog.txt` | 本家のリリースの履歴（git の履歴に残っている） |
| `doc/doxyconfig` | 本家の doxygen 設定（本家のサイトに出すためのもの） |
| `doc/img/` | 本家の `readme.md` に貼っていたゲームの画像 |
| `executing` | 中身が空のファイル |
| `lib/surugacc-O2/`, `lib/surugacc-debug-O2/` | 昔のビルドが `lib/` に作った Z80 ドライバの生成物。今は `surugacc/out/` に作る |
| `bin/xgmRomBuilder.jar`, `bin/xgmRomBuilder.txt` | XGM の音楽 ROM を作る単体の道具。`build.ps1` も rescomp も使わない |
| `project/template/out/rom.bin` | GCC で作ったテンプレートの ROM |
| `res/image/logo_old.png`, `sgdk_logo_2.png`〜`sgdk_logo_4.png` | どこからも使っていないロゴ（`libres.res` は `sgdk_logo.png` だけ） |
| `inc/sprite_eng_legacy.h`, `src/mw/sprite/sprite_eng_legacy.c` | 古いスプライトエンジン。SPRX を組み込んだ標準のエンジンだけにした。`LEGACY_SPRITE_ENGINE` は古いコードのために 0 のまま残す |
| `inc/ext/sprite_eng_ext.h` | SPRX の以前の名前のヘッダー（`sprx.h` を読むだけ）。どこからも使っていない |
| `-DSGDK_GCC` | ライブラリにもサンプルにもこれを見ているところがない |
| `zerasul.genesis-code` のおすすめ（`.vscode/extensions.json`） | GCC と make でビルドする拡張。テンプレートには SurugaCC でビルドする `tasks.json` を足した |

残したもの:

- `license.txt`: SGDK のライセンス（MIT）。ソースの作者の表記も残す
- `COPYING.RUNTIME`: `bin/libgcc_s_dw2-1.dll` を配るためのライセンス（GCC の実行時ライブラリ例外）
- `inc/config.h` の `LEGACY_ERROR_HANDLER` と `LEGACY_FONT_LOCATION`: 今も選べる設定（`LEGACY_FONT_LOCATION` は 1 で使っている）
- ヘッダーの `_Pragma("GCC error ...")`: 使わなくなった API の名前を使ったときにエラーで知らせるための目印。ふだんのビルドには関係しない
- `setup.bat`, `memo.md`, `SGDK.code-workspace`: このフォークで足したもの

## 本家のファイルとの対応

本家の直した点を手で持ってくるときのために、中身を分けたり書き換えたりしたところには `[V1]` の目印があります（`git grep "\[V1\]"`）。主なものは次のとおりです。

| 本家のファイル | 移した先 |
|---|---|
| `inc/vdp.h` のポート・状態・コマンドワード | `inc/hal/hal_vdp.h` |
| `sys.c` の `SYS_doVBlankProcessEx()` | `src/mw/sched.c` |
| `sys.c` の割り込みのフックと `SYS_disableInts()` など | `src/hal/hal_cpu.c` |
| `z80_ctrl.c` の `Z80_init()` とドライバの読み込み | `src/mw/snd/z80_driver.c` |
| `sprite_eng.c` の `SPR_update()` | `SPR_updateDefault()` に名前を変えた（`SPR_update()` は `sprite_port.c`） |
| `tools.c` の `getFPS()` / `getFPS_f()` | `src/mw/sys.c` |
| `dma.c` の `DMA_doDmaFast()` | HAL のマクロで書き直した |
| `boot/sega.s` の V-Int | `jsr BMP_doVBlankProcess` を `vintTaskCB` に |

## 次の段階

1. 実測: `run68k.py --profile`（SurugaCC の tools）で関数ごとのサイクルが取れます。計画書の「現状cycles」を実測に置き換えてから ASM 化の順番を決める。
2. ASM 化の候補（計画書の A1 DMA キュー登録、A2 `SPR_updateDefault` の SAT 作成、A6 パレットのフェード）。
3. 残りの層違反（#7 ライブラリのリソース、#8 DMA キューと Z80）。
