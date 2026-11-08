# 新SPRデータ（JSON）をSGDKに組み込む実装計画

対象: `sample/fz2/res/export/STAGE1/animations.json` ＋ `Stage1Base.png`
更新: 2026-11-08（方針＝**案C: スプライトエンジン拡張** に決定、VRAMモード指定を追加）

---

## 0. 決まったこと

- 方針は **案C**。SGDK のスプライトエンジンに「パーツ単位でタイル番号・反転・パレットを持つコマ形式（拡張コマ）」を追加する。旧 `SPR_MODE1` の後継。
- JSON の parts の `image` が参照する PNG から矩形を切り出し、そのタイルを VRAM に載せる。
- 切り出したタイルの載せ方は、変換時に **RESIDENT（VRAMに置きっぱなし）/ TRANSFER（転送）** のどちらかを選べるようにする。
- 本家更新との衝突を減らすため、`sprite_eng.c` / `sprite_eng.h` への変更は「分岐とフラグ定義の数行」だけにする。処理の本体は新しいファイルに置く。

---

## 1. JSONの解析結果

### 1.1 全体構造

| キー | 内容 | 今回の値 |
|---|---|---|
| `version` | フォーマット版数 | `4` |
| `coordinates.origin` | 座標原点 | `canvasCenter`（キャンバス中心＝オブジェクトの基準点） |
| `coordinates.anchor` | パーツ座標がパーツのどこを指すか | `topLeft` |
| `coordinates.axisX / axisY` | 軸の向き | `right` / `down`（MD の画面座標と同じ） |
| `canvas` | 編集用キャンバス | 256×256 |
| `parts[]` | 切り出しパーツ（絵の部品） | 6個 |
| `frames[]` | パーツを並べた1コマ | 3コマ |
| `animations[]` | コマの再生順と時間 | 1本 |

### 1.2 parts（絵の部品）

```
{ id, width, height,
  source: { image, x, y, width, height },   // 元画像からの切り出し矩形
  attributes: {} }
```

- `source.image` が参照する PNG から矩形を切り出し、そのまま VRAM に載るタイルデータにする。今回の `"(DotArt)"` は同じフォルダの `Stage1Base.png`（120×24, 16色インデックス, 色0=透明）を指す。
- 変換ツールでの PNG の探し方: ① `image` がファイル名なら JSON と同じフォルダから探す。② `"(名前)"` の形なら、.res で指定した PNG を使う（`名前=ファイル` の対応表も指定できる）。③ どちらも無ければエラー。パーツごとに別の PNG を参照してもよい。
- 今回のパーツは、24×24（本体, 3×3タイル）と 8×24（側面, 1×3タイル）が3組（x=0/8, 40/48, 80/88）。
- サイズはすべて8の倍数・32以下なので、1パーツ＝VDPスプライト1枚にそのまま割り当てられる。

### 1.3 frames（コマ）

```
{ index, name, defaultDurationMs,
  sprites: [ { part, x, y, flipX, flipY, palette } ],
  attributes: {} }
```

- `x, y` は、キャンバス中心から見たパーツ左上の位置（ピクセル単位）。
- 各コマは「本体 (-12,-16)」「左側面 (-20,-16)」「右側面＝左側面を flipX したもの (12,-16)」の3枚。
- 外接矩形は x: -20〜+20, y: -16〜+8 → **40×24**。
- 同じパーツを同じコマの中で反転して再利用している。拡張コマならタイルを複製せずにそのまま表現できる。

### 1.4 animations（再生）

```
{ index, name, loop: "loop",
  entries: [ { frame, durationMs } ],
  attributes: {} }
```

- 今回は frame 0→1→2→1 を各100msでループ（今のコードの `AnimIndexOffset` と同じ並び。ただし今のコードは24フレーム＝400ms）。
- `loop` はエディタツール側の仕様なので、変換ツールは読み飛ばす（ユーザー確認済み）。
- 同じコマを複数回参照する（frame 1 が2回）。

### 1.5 まだ入っていないもの

- 当たり判定: なし。
- `attributes`（parts / frames / animations）: **ユーザー定義の領域**で、将来パラメータなどを拡張するために使う。変換ツールは今は読み飛ばす（中身があってもエラーにしない）。
- パレットの色: JSON には無く、PNG のパレットから取る。
- 優先度（priority）: なし。

---

## 2. 拡張コマ形式（エンジン側）

### 2.1 データ構造

標準の `AnimationFrame` の形は**変えない**。拡張コマでは、標準の `frameVDPSprites[]` の**直後に**パーツごとの追加情報の配列を置く。

```c
// inc/ext/sprite_eng_ext.h（新規）
typedef struct
{
    u16 tileOffset;   // スプライトの VRAM 基準位置からのタイル番号
    u16 attr;         // bit0: flipX, bit1: flipY, bit2-3: palette (相対)  ※ VDP の属性ビットに変換済みで持ってもよい
} FrameVDPSpriteExt;

// 拡張コマのメモリ上の並び:
//   AnimationFrame ヘッダー (numSprite, timer, tileset, collision)
//   FrameVDPSprite    frameVDPSprites[n]   ← 標準と同じ。可視判定・外接矩形計算はそのまま動く
//   FrameVDPSpriteExt ext[n]               ← 追加分。&frameVDPSprites[n] から取れる
```

- 標準部分の `offsetX/offsetXFlip/offsetY/offsetYFlip/size` は変換ツールが計算する。これまでどおり使えるので、`updateVisibility` などは変更しなくてよい。
- 標準の `numTile` は拡張コマでは使わない（0）。タイル位置は `ext[i].tileOffset` で指定する。
- パーツの反転は「パーツ自身の flip」と「スプライト全体の flip」の XOR で決める。全体を反転したときの位置は `offsetXFlip/offsetYFlip` で計算済み。
- パレット番号: スプライトに設定したパレット（PAL0〜3）＋パーツの `palette`（4で割った余り）。今のデータは全部0なので、スプライトに設定したパレットがそのまま使われる。
- パレットの色: SpriteDefinition には持たせない。.res の PALETTE と同じ形で別に管理する（4.6）。

### 2.2 拡張コマかどうかの判定

- Sprite のフラグに `SPR_FLAG_EXT_FRAME`（空いているビット `0x0080`）を追加する。
- `SPR_addSpriteEx` / `SPR_setDefinition` に拡張用の SpriteDefinition を渡すときは、ランタイム関数（5章の `SPRX_*`）がこのフラグを立てる。

### 2.3 `sprite_eng.c` / `sprite_eng.h` への変更（これだけにする）

1. `sprite_eng.h`: `#define SPR_FLAG_EXT_FRAME 0x0080` を追加し、`SPR_FLAG_MASK` に加える（2行）。
2. `sprite_eng.c` `SPR_update()` の SAT（スプライトテーブル）を作る部分で、`numSprite < 0` の特別処理の前に分岐を1つ入れる:

```c
#if SPR_EXT_FRAME_ENABLE
            if (status & SPR_FLAG_EXT_FRAME)
                vdpSprite = SPRX_fillVDPSprites(sprite, vdpSprite, &vdpSpriteInd);
            else
#endif
            if (numSprite < 0) { ... 既存のまま ... }
```

3. `config.h` に `#define SPR_EXT_FRAME_ENABLE 1`（0 にすると本家と同じ動きに戻る）。

`loadTiles()` は変更しない。TRANSFER モードでは、コマの `tileset` に「そのコマで使うパーツのタイル（重複なし）」を入れておけば、既存の DMA 転送がそのまま使える。RESIDENT モードでは `tileset` の枚数を0にして転送させない。

`SPRX_fillVDPSprites()` の本体は新規ファイル `src/ext/sprite_eng_ext.c` に置く。中身は既存の4種類の反転ループと同じで、違いは「`attr += numTile` の代わりに `attr = base + ext.tileOffset`、flip とパレットのビットを ext から合成する」点だけ。

本家の `sprite_eng.c` が更新されたときは、上の分岐1か所を入れ直せばよい。マージで衝突しても範囲が小さい。

### 2.4 制約（変換ツールで検査する）

| 項目 | 制約 |
|---|---|
| 1コマの VDP スプライト数 | 16以下（visibility が u16） |
| オフセット | 外接矩形の中で 0〜255（u8） |
| パーツのサイズ | 8の倍数、32×32以下（大きいパーツは将来自動分割） |
| 時間 | `timer` は u8 のフレーム数。`durationMs ÷ 時間の割る数`（.res のパラメータ、4.2）を四捨五入。1〜255 に収まらなければエラー（0 になる場合は 1 にして警告） |
| ループ | JSON の `loop` は無視。SGDK の `loop` は常に 0（先頭に戻る）。止めたいときは実行時に `SPR_setAnimationLoop(spr, FALSE)` |

---

## 3. VRAM モード（置きっぱなし / 転送）

`.res` で変換時に指定する。どちらのモードでもコマのデータ形式は同じ（2.1）。違うのは、タイルがどこにあるかと、`tileOffset` が何を基準にした値かだけ。

| | RESIDENT（置きっぱなし） | TRANSFER（転送） |
|---|---|---|
| 載せ方 | 全パーツのタイルを最初に1回だけ VRAM に置く。全コマで共有 | コマで使うパーツだけを、コマが変わるたびに DMA で転送 |
| `tileOffset` の基準 | 置きっぱなし領域の先頭 | そのコマの転送先の先頭 |
| コマの `tileset` | 0枚（転送しない） | そのコマのパーツのタイル（同じパーツは1回だけ） |
| VRAM 量（今回のデータ） | 全パーツ 9+3 ×3 = **36タイル** | 1コマ最大 9+3 = **12タイル** |
| 複数体出したとき | 全員で共有（体数に関係なく一定） | 1体ごとに 12タイル |
| 毎フレームの負荷 | ほぼ無し | コマ切り替え時に DMA |
| 向いているもの | 基地・敵など同じ絵が複数ある、パーツ数が少ないもの | 自機など1体だけ、パーツ数が多いもの |
| 圧縮 | BEST 可（ロード時に1回だけ展開） | NONE か FAST 推奨（BEST のときは警告） |

参考: 今の手書き版（`Stage1_base_tileset`）は45タイル、案Aは置きっぱなしで45タイルだった。拡張コマでは反転パーツを複製しないので、置きっぱなしで36タイル、転送で12タイルになる。

RESIDENT では、同じ絵のパーツ（反転すると一致するものも含む）を変換時に1つにまとめる。

---

## 4. 変換ツール（C#）

### 4.1 置き場所

- `tools/rescomp-cs/src/Processors/SpriteJsonProcessor.cs`（新規）
- `tools/rescomp-cs/src/Types/SprJson.cs`（新規、JSON のモデルと検査）
- `tools/rescomp-cs/src/Resources/SpriteExt.cs`（新規、拡張コマの出力）
- 既存ファイルの変更は `Compiler.cs` のプロセッサ一覧に1行足すだけ。rescomp-cs はこのフォーク独自のツールなので、本家とは衝突しない。

### 4.2 .res の書式

```
SPRJ name "json" vram_mode [compression [timediv [png]]]

SPRJ stage1_base "export/STAGE1/animations.json" RESIDENT BEST 4.1667 "export/STAGE1/Stage1Base.png"
SPRJ opaopa_new  "export/OBJECT/opaopa.json"     TRANSFER FAST
```

- `vram_mode`: `RESIDENT` / `TRANSFER`（3章）。**省略不可**。無い・綴りが違うときは変換エラー。
- `compression`: 既存の SPRITE と同じ（NONE / APLIB / FAST / BEST）。
- `timediv`: `durationMs` をこの値で割ってフレーム数（`timer`）にする（ユーザー指定）。小数可。
  - 省略時は `16.6667`（1000/60、NTSC で実時間どおり。100ms→6フレーム）。
  - `20` にすると PAL で実時間どおり（100ms→5）。
  - 上の stage1_base の例の `4.1667` は、今の基地と同じ 100ms→24フレームにする値。
  - `0` はアニメ時間を使わない（`timer=0`、自動アニメ無効。コマ送りはゲーム側で行う）。
- `png`: JSON の `image` が実ファイルを指さないときだけ指定（1.2）。

### 4.3 処理の流れ

1. JSON を `System.Text.Json` で読み、`version` を確認する（4 以外は警告）。
2. 参照されている PNG を既存の `Imaging` で 8bpp インデックスとして読む（16色を超える・色0以外が透明、はエラー）。
3. パーツごとに `source` の矩形を切り出してタイルにする。RESIDENT のときは、同じ絵（反転して一致するものも含む）を1つにまとめる。
4. 各コマ:
   - 全スプライトの外接矩形を計算し、**基準点を中心に左右対称になるよう広げる**（スプライト全体を反転しても基準点がずれないように）。
   - パーツごとに `FrameVDPSprite`（size、通常／反転時のオフセット）と `FrameVDPSpriteExt`（tileOffset、flip、palette）を作る。
   - TRANSFER のときは、そのコマで使うパーツのタイルを並べて `TileSet` を作る（既存の Tileset 出力と圧縮をそのまま使う）。
5. アニメ: `entries` の順に `AnimationFrame*` を並べる。同じコマは同じポインタを共有する。`durationMs ÷ timediv` を四捨五入して `timer` にする。
6. `SpriteDefinition`: w/h＝全コマの外接矩形の最大、`maxNumSprite` を計算する。`maxNumTile` は TRANSFER なら1コマの最大、RESIDENT なら 0（エンジンの自動 VRAM 確保を使わせない）。palette は `ラベル_palette`（4.6）を指す。
7. 出力: `.s`（データ）、`.h`（下記）、`.d`（依存ファイルとして JSON と PNG）。

### 4.4 ヘッダーに出すもの

```c
extern const SprxInfo stage1_base;   // これ1つで扱えるようにする

// 型は inc/ext/sprite_eng_ext.h（新規）
typedef enum { SPRX_RESIDENT = 0, SPRX_TRANSFER = 1 } SprxVramMode;
typedef struct {
    const SpriteDefinition* def;
    u16 vramMode;               // .res で指定した値
    u16 vramTiles;              // RESIDENT: 全パーツのタイル数 / TRANSFER: 1コマの最大
    const TileSet* residentTiles; // RESIDENT のとき置きっぱなしにするタイル（TRANSFER は NULL）
    s16 pivotX, pivotY;         // 基準点（キャンバス中心）の、外接矩形の中での位置
} SprxInfo;
```

### 4.6 パレットの取り込み（ユーザー指定）

- パレットの色は .res の `PALETTE` で別に管理する方針。そのため、変換ツールは画像を取り込むときに、PNG のパレットを **`ラベル_palette`** という名前の Palette リソースとして出力する。
  - 例: `SPRJ stage1_base ...` → `extern const Palette stage1_base_palette;`
  - 中身は `PALETTE stage1_base_palette "export/STAGE1/Stage1Base.png"` と書いたときと同じ（16色）。
- 1つの JSON が複数の PNG を参照していて、パレットが違う場合: 最初の PNG を `ラベル_palette`、2つ目以降を `ラベル_palette1`, `ラベル_palette2` … とし、警告を出す。
- `SpriteDefinition.palette` には `ラベル_palette` へのポインタを入れておく（SGDK の標準 API との互換のため）。ゲーム側では使わなくてよい。
- 実行時は、ほかの PALETTE と同じように `PaletteWork` へコピーする。例: `memcpy(&PaletteWork[PAL1], stage1_base_palette.data, 16*2);`
- 同じ名前の `PALETTE` が .res に手で書いてあると名前が衝突する。その場合は変換時にエラーにする。

### 4.5 テスト

- 単体テスト: 今回の JSON から、オフセット、tileOffset、タイル数（RESIDENT 36 / TRANSFER 12）、timediv 省略時 timer=6・timediv=4.1667 で timer=24、loop=0 を確認する。
- 目視: 変換結果を PNG に描き戻す `--preview` オプションを付け、エディタの表示と見比べる。
- 同じ入力から同じ出力になることを確認する。

---

## 5. 実行時の API と fz2 への組み込み

### 5.1 ランタイム（`src/ext/sprite_eng_ext.c` / `inc/ext/sprite_eng_ext.h`、新規）

```c
// RESIDENT 用: 置きっぱなしのタイルを1回だけ VRAM に載せる（以前決めた VRAM_alloc 方式）
u16    SPRX_loadResident(const SprxInfo* info, VRAMRegion* region, TransferMethod tm); // 戻り値 = VRAM の先頭
// スプライトを作る。モードに応じて SPR_FLAG_EXT_FRAME と、自動転送 / VRAM 番号を設定する
Sprite* SPRX_addSprite(const SprxInfo* info, s16 x, s16 y, u16 attr, u16 vramIndex);
bool    SPRX_setDefinition(Sprite* spr, const SprxInfo* info, u16 vramIndex);  // fz2 のスプライトプール用
// 基準点の座標で位置を指定する
void    SPRX_setPosition(Sprite* spr, const SprxInfo* info, s16 x, s16 y);
// エンジンから呼ばれる（2.3）
VDPSprite* SPRX_fillVDPSprites(Sprite* spr, VDPSprite* vdp, s16* vdpSpriteInd);
```

- RESIDENT: `vramIndex` には `SPRX_loadResident` の戻り値を渡す。何体作っても同じ値でよい。自動転送はオフにする。
- TRANSFER: `vramIndex` には `VRAM_alloc(region, info->vramTiles)` で確保した位置を渡す。`SPR_FLAG_AUTO_TILE_UPLOAD` をオンにする（今の OpaOpa と同じ方式）。

### 5.2 fz2 での使い方（基地の例）

```c
// BGSetupArea で1回だけ
s_baseVram = SPRX_loadResident(&stage1_base, &m_Stage_Control.region_spritesFixed, DMA);

// 各基地
Sprite* spr = GetSpriteBuffer();
SPRX_setDefinition(spr, &stage1_base, s_baseVram);
SPR_setPalette(spr, PAL1);

// 毎フレーム
SPRX_setPosition(spr, &stage1_base, scr.x, scr.y);
```

- 自動アニメ（`timer`）が使えるので、今の `m_NowFrame` / `AnimIndexOffset` の手動管理は要らなくなる。

### 5.3 置き換える場所

- `src/SpriteTables.c` の `#if 0` の `base_spr` 手書きテーブル → 削除（生成物に置き換え）。
- `src/BG.c` の `#ifndef SPR_ON` の部分と `SPR_MODE1` → 5.2 の形で書き直す。
- `res/res_gfx.res` の `TILESET Stage1_base_tileset ...` → `SPRJ stage1_base ...`（Stage2〜4 も同様）。
- `StageTables.c` の `m_StageBaseData` を `const SprxInfo*` にする。

---

## 6. 本家更新を取り込みやすくするための約束

- 本家のファイルへの変更は次の3か所だけ。どれも数行:
  - `inc/sprite_eng.h`: フラグ定義とマスク（2行）
  - `src/sprite_eng.c`: `SPR_update()` の分岐（数行、`#if SPR_EXT_FRAME_ENABLE` で囲む）
  - `inc/config.h`: `SPR_EXT_FRAME_ENABLE`（1行）
- それ以外は新規ファイルに置く: `src/ext/`、`inc/ext/`、rescomp-cs の Processor/Types/Resources、ドキュメント。
- 本家の変更箇所には `// [EXT] SPRX` のような目印のコメントを付け、マージ時に grep で見つけられるようにする。
- 本家が `SPR_update()` や `FrameVDPSprite` を変えたら、`sprite_eng_ext.c` のループを追従させる。手順をドキュメントに書いておく。
- `SPR_EXT_FRAME_ENABLE 0` でビルドしたときに、本家と同じ動きになることを確認するテストを用意する。

---

## 7. 作業ステップ

1. エンジン: `sprite_eng_ext.h/.c` と、`sprite_eng.c` の分岐。手書きの拡張コマで表示確認（反転、パレット、全体反転、画面端での可視判定）。
2. 変換ツール: JSON モデルと検査、PNG 切り出し、パーツのまとめ（RESIDENT）。
3. 変換ツール: 拡張コマの出力（RESIDENT / TRANSFER）、`.res` への `SPRJ` 登録、`.h/.d` の出力。
4. 単体テストとプレビュー出力。
5. fz2: `res_gfx.res` の変更、`BG.c` の書き直し、`SpriteTables.c` の整理。MAME で確認する。
6. （将来）`attributes` のユーザー定義パラメータを出力する仕組みを決める（形式が決まってから）。コマの `collision` は今は NULL。
7. ライブラリの再ビルド手順（`build_lib`）と surugacc のビルドへの組み込み。

---

## 8. 確認事項（すべて決定済み）

1. ~~`palette` の意味~~ → 決定。パレットの色は .res の PALETTE で管理し、取り込むときに `ラベル_palette` として出力する（4.6）。パーツの `palette` 番号は、スプライトのパレットからの相対値として扱う（今のデータは全部0）。
2. ~~当たり判定 / attributes~~ → 決定。`attributes` はユーザー定義で将来の拡張用。今は読み飛ばす。
3. ~~`loop` の値~~ → 決定。ツールの仕様なので無視する。
4. ~~時間~~ → 決定。.res の `timediv` パラメータで `durationMs` を割ってフレーム数にする（4.2）。
5. ~~VRAM モードの既定値~~ → 決定。省略不可（書かれていなければ変換エラー）。
