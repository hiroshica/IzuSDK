#include <common.h>
// clang-format off
#pragma GCC push_options
#pragma GCC optimize ("O0")
//-----------------------------------------------------------------------------------------------------------------
// ここでいうanimationとはタイムテーブルの個数（opaopaでいうならエンジンの数(6)とエンジン別の足パターンなので6x2=12)
// 初期化用ダミー
SpriteDefinition sp1x1 = {
    8,8,        // 表示ドット数 W/H
    NULL,       // Palette data pointer
    0,          // animationの数
    NULL,       // animation data pointer
    0,          // 使用Tile数
    0           // 表示Sprite数
};
#if 0
// 基地用
TileSet base_spr1_tile =
{
    0,          // compression;
    15,         // numTile;
    NULL        // *tiles;
};
//
#endif
#if 0     //////////////////////////////////////////
AnimationFrameMode1 base_spr1_af1_mode1_0 = 
{
    2,                  // numSprite このテーブルで使用しているSpriteの数(data = frameVDPSprites)
    24,                 // animation速度（Frame単位)
    NULL,//&base_spr1_tile,    // 転送する場合のTile Data
    NULL,               // この表示のコリジョンデータ
    // frameVDPSpritesMode1
    {
        {
            SPRITE_SIZE(4,3),
            (15*0),
            -20,-12,
        },
        {
            SPRITE_SIZE(1,3),
            (15*2)+12,
            12,-12,
        }
    },
    // frameVDPSpritesMode1
};
AnimationFrameMode1 base_spr1_af1_mode1_1 = 
{
    2,                  // numSprite このテーブルで使用しているSpriteの数(data = frameVDPSprites)
    24,                 // animation速度（Frame単位)
    NULL,//&base_spr1_tile,    // 転送する場合のTile Data
    NULL,               // この表示のコリジョンデータ
    // frameVDPSpritesMode1
    {
        {
            SPRITE_SIZE(4,3),
            (15*1),
            -20,-12,
        },
        {
            SPRITE_SIZE(1,3),
            (15*0)+12,
            12,-12,
        }
    },
    // frameVDPSpritesMode1
};
AnimationFrameMode1 base_spr1_af1_mode1_2 = 
{
    2,                  // numSprite このテーブルで使用しているSpriteの数(data = frameVDPSprites)
    24,                 // animation速度（Frame単位)
    NULL,//&base_spr1_tile,    // 転送する場合のTile Data
    NULL,               // この表示のコリジョンデータ
    // frameVDPSpritesMode1
    {
        {
            SPRITE_SIZE(4,3),
            (15*2),
            -20,-12,
        },
        {
            SPRITE_SIZE(1,3),
            (15*1)+12,
            12,-12,
        }
    },
    // frameVDPSpritesMode1
};
//

AnimationFrame *base_spr1_af[] =
{
    (AnimationFrame *)&base_spr1_af1_mode1_0,
    (AnimationFrame *)&base_spr1_af1_mode1_1,
    (AnimationFrame *)&base_spr1_af1_mode1_2,
    (AnimationFrame *)&base_spr1_af1_mode1_1,
};

Animation base_spr1_a1 = 
{
    4,              // numFrame = このタイムテーブルに存在するキーフレーム数
    0,              // loop
    &base_spr1_af[0]   // key Frame Data pointer
};
Animation *base_spr1_a[] =
{
    &base_spr1_a1,
    //&base_spr1_a1,
};
SpriteDefinition base_spr = 
{
    8*5,8*3,            // 表示ドット数 W/H
    NULL,               // Palette data pointer
    1,                  // animationの数
    &base_spr1_a[0],    // animation data pointer
    5*3,                // 使用Tile数
    2                   // 表示Sprite数
};

// clang-format on
#endif