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
// 基地用の手打ちテーブル (base_spr / AnimationFrameMode1) は SPRJ に置き換えたので削除
// (res_gfx.res の stage1_base / export/STAGE1/animations.json)

// clang-format on
