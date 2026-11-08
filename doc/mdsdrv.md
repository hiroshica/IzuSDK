# MDSDRV（音のドライバ）

IzuSDK の音のドライバは MDSDRV（Ian Karlsson 作、zlib ライセンス）だけです。XGM / XGM2 は外しました。

## ライブラリにあるもの

- `inc/snd/mdsdrv.h`, `src/mw/snd/mdsdrv.c`: MDSDRV を呼ぶ関数（`MDS_init`, `MDS_request`, `MDS_command`, `MDS_command2`, `MDS_update`, `MDS_get_version_str`, `MDS_pause`, `MDS_fade`）。`genesis.h` から読まれます。
- `res/sound/mdsdrv.bin`: ドライバ本体（`res/libres.res` の `mdsdrvdat`）。

## プロジェクトで用意するもの

曲と効果音は mdslink（MDSDRV の道具）で MML / MDS から作り、プロジェクトの `.res` に入れます。

```
BIN mdsseqdat "snd/mdsseq.bin" 2 2 0 NONE FALSE
BIN mdspcmdat "snd/mdspcm.bin" 32768 2 0 NONE FALSE
```

mdslink が作る `mdsseq.h` には曲の番号が入っています。作り方の例は Tokaido の `samples/megadrive/fz2/SndBase/build.bat` です。

ドライバ本体はライブラリのものを使うので、mdslink はドライバと同じ版のものを使ってください（版が違うと `MDS_init()` が 0 以外を返します）。

## 使い方

```c
#include <genesis.h>
#include "mdsdat.h"   // mdsseqdat, mdspcmdat
#include "mdsseq.h"   // 曲の番号

if (MDS_init(mdsseqdat, mdspcmdat))
{
    // ドライバと曲のデータの版が合っていない
}
MDS_request(MDS_BGM, 1);   // BGM を鳴らす (0 で止める)
MDS_request(MDS_SE1, 5);   // 効果音
```

- `MDS_init()` が成功すると、`MDS_update()` は Z80 の V-Int コールバック（`Z80_setVIntCallback`）に登録され、毎フレーム自動で呼ばれます。自分で `MDS_update()` を呼ぶ必要はありません（呼ぶと 1 フレームに 2 回進みます）。
- ほかの Z80 ドライバ（`SND_PCM_loadDriver()` など）を読み込むと、その登録は外れます。`Z80_getLoadedDriver()` は MDSDRV のとき `Z80_DRIVER_MDSDRV` を返します。
- デバッグビルド（`DEBUG` が定義されているとき）では、もとのラッパーと同じく `MDS_update()` は何もしません。
