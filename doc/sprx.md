# SPRX（このフォークのメインのスプライト）と SPRJ リソース

このフォーク独自の機能です（本家 SGDK にはありません）。V1 からは拡張ではなく、標準のスプライトエンジンに組み込んだメインのスプライトの道です。
JSON 形式のスプライトデータ（パーツ / コマ / アニメ / 当たり矩形）を、rescomp-cs の `SPRJ` リソースで変換し、
拡張したスプライトエンジンで表示します。

- パーツ（VDP スプライト 1 枚）ごとに、タイル位置・左右/上下反転・パレットを持てます。同じパーツを反転して使ってもタイルは複製しません。
- タイルの載せ方を変換時に選べます: **RESIDENT**（VRAM に置きっぱなし・全員で共有）/ **TRANSFER**（コマが変わるたびに DMA 転送）。
- JSON の `boxes`（当たり関連の矩形）を出力し、`kind` を `SprxBoxKind` の enum にします。

## .res の書き方

```
SPRJ_KINDS "export/box_kinds.txt"     // 任意。SPRJ より前に 1 回だけ
SPRJ name "json" vram_mode [compression [timediv [png]]]
```

| 引数 | 内容 |
|---|---|
| `name` | `SprxInfo` の変数名。ほかに `name_def`（SpriteDefinition）と `name_palette`（Palette）も出力します |
| `json` | JSON ファイル（version 4） |
| `vram_mode` | **省略不可**。`RESIDENT` / `TRANSFER` |
| `compression` | `NONE`（既定）/ `APLIB` / `FAST` / `BEST`。TRANSFER は毎回展開するので NONE か FAST を推奨（BEST/APLIB は警告） |
| `timediv` | `durationMs` をこの値で割って四捨五入した値をコマの時間（フレーム数）にします。既定 `16.6667`（1000/60、100ms→6）。`20` は PAL の実時間、`0` は自動アニメなし。結果が 255 を超えるとエラー、0 になるときは 1（警告） |
| `png` | パーツの `source.image` がファイル名でないとき（例 `"(DotArt)"`）に使う画像。1 つのファイル名か、`名前=ファイル` を `,` で区切って並べたもの（例 `"DotArt=a.png,Other=b.png"`） |

- `source.image` がファイル名（拡張子つき）なら、JSON と同じフォルダ、次に .res のフォルダから探します。
- `name_palette` は PNG のパレット（16 色）です。中身は `PALETTE name_palette "png"` と書いたのと同じです。同じ名前の PALETTE を .res に書くとエラーになります。
- JSON の `attributes` と `loop` は読み飛ばします（アニメは常に先頭に戻ってループします。止めたいときは `SPR_setAnimationLoop(spr, FALSE)`）。

### 当たり矩形の kind

`SPRJ_KINDS` のファイルには 1 行に 1 つ kind を書きます（`//` と `#` で始まる行は無視）。書いた順に 0, 1, 2… になります。
JSON にファイルに無い kind があるとエラーになります。

`SPRJ_KINDS` が無いときは、その .res に出てくる kind を名前順に並べます（.res ごとに番号が変わることがあるので、.res が複数あるなら `SPRJ_KINDS` を使ってください）。

```c
#ifndef SPRX_BOX_KIND_DEFINED
#define SPRX_BOX_KIND_DEFINED
typedef enum
{
    SPRX_KIND_HIT = 0,    // "hit"
    SPRX_KIND_ATTACK = 1,    // "attack"
    SPRX_KIND_MAX = 2
} SprxBoxKind;
#endif
```

名前は `SPRX_KIND_` ＋ kind を大文字にしたもの（英数字以外は `_`）です。

### ヘッダーに出るもの

```c
extern const Palette name_palette;
extern const SpriteDefinition name_def;
extern const SprxInfo name;
#define name_VRAM_TILES 36            // RESIDENT: 置きっぱなしのタイル数 / TRANSFER: 1 体あたりのタイル数
#define name_ANIM_ANIMATION_1 0       // JSON のアニメ名から
```

## 実行時の使い方

`genesis.h` から `sprx.h` がいつも読まれます（標準のスプライトエンジンを使っているとき。`LEGACY_SPRITE_ENGINE` を 1 にした古いエンジンでは使えません）。
以前の `ext/sprite_eng_ext.h` も、`sprx.h` を読むだけのファイルとして残してあります。

### RESIDENT（同じ絵を何体も出すもの）

```c
// 1 回だけ: 置きっぱなしのタイルを VRAM に載せる
s16 vram = SPRX_loadResident(&stage1_base, &region_spritesFixed, DMA);   // 失敗すると -1

// 何体でも同じ vram を渡す
Sprite* spr = SPRX_addSprite(&stage1_base, x, y, TILE_ATTR(PAL1, FALSE, FALSE, FALSE), vram, 0);
```

### TRANSFER（1 体だけ・コマ数が多いもの）

```c
s16 vram = VRAM_alloc(&region_spritesFixed, opaopa_new.vramTiles);
Sprite* spr = SPRX_addSprite(&opaopa_new, x, y, TILE_ATTR(PAL2, FALSE, FALSE, FALSE), vram, 0);
```

### スプライトプール（`SPR_addSprite` 済みの Sprite を使い回す場合）

```c
SPRX_setDefinition(spr, &stage1_base, vram);
// 普通の SpriteDefinition に戻すときは先に
SPRX_detach(spr);
```

### 位置と当たり矩形

```c
SPRX_setPosition(spr, &stage1_base, x, y);    // x, y = 基準点（エディタのキャンバス中心）

SprxRect r;
if (SPRX_getBox(spr, SPRX_KIND_HIT, 0, &r))   // 基準点からの相対。スプライトの反転を反映済み
{
    // 当たり: (x + r.x, y + r.y) から r.w x r.h
}
```

アニメ・反転・パレット・優先度などは、普通の `SPR_*` 関数がそのまま使えます。

## データ形式

拡張コマは、標準の `AnimationFrame`（`frameVDPSprites[numSprite]` まで）のすぐ後ろに、`FrameVDPSpriteExt[numSprite]`（各 `u16`）を置いたものです。

| bit | 内容 |
|---|---|
| 0-10 | スプライトの VRAM タイル番号からのタイル位置 |
| 11 | このパーツの左右反転（スプライト全体の反転と XOR） |
| 12 | このパーツの上下反転（同上） |
| 13-14 | パレットのずらし量（スプライトのパレットに足して 4 で割った余り） |

- 標準部分のオフセット・サイズは普通に計算してあるので、エンジンの可視判定はそのまま動きます。
- スプライトの外接矩形は基準点を中心に左右・上下対称にしてあります。全体を反転しても基準点の位置は変わりません。
- RESIDENT のコマの `tileset` はタイル 0 枚（転送しない）、TRANSFER のコマの `tileset` はそのコマで使うパーツのタイル（同じパーツは 1 回だけ）です。
- 当たり矩形は `AnimationFrame.collision` に `SprxBoxList*` として入れてあります（エンジンはこの欄を使っていません）。

## ファイルの場所（V1）

| ファイル | 内容 |
|---|---|
| `inc/sprx.h` | SPRX の API（以前の `inc/ext/sprite_eng_ext.h`） |
| `src/mw/sprite/sprx.c` | SPRX の実行時の処理（以前の `src/ext/sprite_eng_ext.c`） |
| `src/mw/sprite/sprite_eng.c` | 標準のスプライトエンジン。`SPR_update()` の SAT を作る部分で拡張コマなら `SPRX_fillVDPSprites` を呼ぶ |
| `inc/mw/sprite_port.h`, `src/mw/sprite/sprite_port.c` | スプライトエンジンのポート。`SPR_update()` だけは実行中に差し替えられる（`SPR_setEnginePort`） |
| `inc/sprite_eng.h` | `SPR_FLAG_EXT_FRAME`（0x0080）と `SPR_FLAG_MASK` への追加 |
| `inc/config.h` | `MODULE_SPRITE_EXT`（互換のため残した。`LEGACY_SPRITE_ENGINE == 0` のとき 1） |

本家が `SPR_update()` の SAT 作成部分、`FrameVDPSprite`、`AnimationFrame`、`Sprite` の status ビットを変えたときは、
`src/mw/sprite/sprx.c` の `SPRX_fillVDPSprites()` と rescomp-cs の `SprxFrameRes` を合わせて直してください。
本家の `SPR_update()` は、このフォークでは `SPR_updateDefault()` という名前になっています（`SPR_update()` はポートを通して呼びます）。

### スプライトエンジンを差し替える

```c
static void myUpdate(void)
{
    // 自前の処理 ...
    SPR_updateDefault();        // 標準のエンジンも使うなら呼ぶ
}
static const SpriteEnginePort myPort = { "mine", myUpdate };

SPR_setEnginePort(&myPort);     // NULL で標準 (sprEnginePortDefault) に戻る
```

差し替えられるのは 1 フレームに 1 回呼ぶ `SPR_update()` だけです（関数ポインタ 1 回分、約 16 サイクル）。
`SPR_setPosition()` などの個々の関数は差し替えず、直接呼びます（計画書の R11: 個々の関数を関数ポインタにすると遅くなるため）。

## テスト

`tools/rescomp-cs/tests/sprj/run_test.sh`（Linux）で、変換結果と `src/mw/sprite/sprx.c` を m68k 向けにビルドし、qemu-m68k で動かして確認します。
必要なもの: dotnet 8 SDK、`m68k-linux-gnu-gcc`、`qemu-m68k`（Ubuntu なら `dotnet-sdk-8.0 gcc-m68k-linux-gnu qemu-user`）。
