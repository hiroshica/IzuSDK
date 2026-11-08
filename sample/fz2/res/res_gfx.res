//---------------------------------------------------------------------
// ASCII
//---------------------------------------------------------------------
PALETTE palette_ascii "export/ascii.png"
TILESET basic_tileset1_0 "export/basic.png" BEST NONE
TILESET ascii_tileset1_0 "export/ascii.png" BEST NONE


//---------------------------------------------------------------------
// OPA-OPA
//---------------------------------------------------------------------
SPRITE opaopa "export/OBJECT/OPAOPA.png" 3 2 FAST 12
//SPRITE test "export/OBJECT/test.png" 1 1 BEST
SPRITE test2 "export/OBJECT/test2.png" 5 3 BEST 24 NONE

//---------------------------------------------------------------------
// TITLE
//---------------------------------------------------------------------
SPRITE title_A1 "export/TITLE/title_A1.png" 12 11 BEST 1
SPRITE title_A2 "export/TITLE/title_A2.png" 12 11 BEST 1
SPRITE title_B "export/TITLE/title_B.png" 20 1 BEST 1

//---------------------------------------------------------------------
// Stage1
//---------------------------------------------------------------------
PALETTE palette1_0 "export/BG/1_0_fg.png"
TILESET bga_tileset1_0 "export/BG/1_0_fg.png" BEST ALL
MAP bga_map1_0 "export/BG/1_0_fg.png" bga_tileset1_0 BEST BEST 0
TILESET bgb_tileset1_0 "export/BG/1_0_bg.png" BEST ALL
MAP bgb_map1_0 "export/BG/1_0_bg.png" bgb_tileset1_0 BEST BEST 0

PALETTE palette1_1 "export/BG/1_1_fg.png"
TILESET bga_tileset1_1 "export/BG/1_1_fg.png" BEST ALL
MAP bga_map1_1 "export/BG/1_1_fg.png" bga_tileset1_1 BEST BEST 0
TILESET bgb_tileset1_1 "export/BG/1_1_bg.png" BEST ALL
MAP bgb_map1_1 "export/BG/1_1_bg.png" bgb_tileset1_1 BEST BEST 0

PALETTE palette1_2 "export/BG/1_2_fg.png"
TILESET bga_tileset1_2 "export/BG/1_2_fg.png" BEST ALL
MAP bga_map1_2 "export/BG/1_2_fg.png" bga_tileset1_2 BEST BEST 0
TILESET bgb_tileset1_2 "export/BG/1_2_bg.png" BEST ALL
MAP bgb_map1_2 "export/BG/1_2_bg.png" bgb_tileset1_2 BEST BEST 0

//---------------------------------------------------------------------
// Stage1 Base
//---------------------------------------------------------------------
// 旧: 手打ちテーブル(SpriteTables.c の base_spr)用のタイル
//TILESET Stage1_base_tileset "export/OBJECT/Stage1_Base.png" BEST NONE
// 新: JSON スプライト (SPRJ)。タイルは VRAM に置きっぱなし (基地は全員で共有)
//   timediv 16.6667 = durationMs(100ms) を 1/60 秒単位に (6 フレーム)。旧コードと同じ 24 フレームにするなら 4.1667
SPRJ stage1_base "export/STAGE1/animations.json" RESIDENT BEST 16.6667 "export/STAGE1/Stage1Base.png"

//---------------------------------------------------------------------
// Stage2
//---------------------------------------------------------------------
PALETTE palette2_0 "export/BG/2_0_fg.png"
TILESET bga_tileset2_0 "export/BG/2_0_fg.png" BEST ALL
MAP bga_map2_0 "export/BG/2_0_fg.png" bga_tileset2_0 BEST BEST 0
TILESET bgb_tileset2_0 "export/BG/2_0_bg.png" BEST ALL
MAP bgb_map2_0 "export/BG/2_0_bg.png" bgb_tileset2_0 BEST BEST 0

PALETTE palette2_1 "export/BG/2_1_fg.png"
TILESET bga_tileset2_1 "export/BG/2_1_fg.png" BEST ALL
MAP bga_map2_1 "export/BG/2_1_fg.png" bga_tileset2_1 BEST BEST 0
TILESET bgb_tileset2_1 "export/BG/2_1_bg.png" BEST ALL
MAP bgb_map2_1 "export/BG/2_1_bg.png" bgb_tileset2_1 BEST BEST 0

PALETTE palette2_2 "export/BG/2_2_fg.png"
TILESET bga_tileset2_2 "export/BG/2_2_fg.png" BEST ALL
MAP bga_map2_2 "export/BG/2_2_fg.png" bga_tileset2_2 BEST BEST 0
TILESET bgb_tileset2_2 "export/BG/2_2_bg.png" BEST ALL
MAP bgb_map2_2 "export/BG/2_2_bg.png" bgb_tileset2_2 BEST BEST 0

//---------------------------------------------------------------------
// Stage2 Base
//---------------------------------------------------------------------
TILESET Stage2_base_tileset "export/OBJECT/Stage1_Base.png" BEST NONE

//---------------------------------------------------------------------
// Stage3
//---------------------------------------------------------------------
PALETTE palette3_0 "export/BG/3_0_fg.png"
TILESET bga_tileset3_0 "export/BG/3_0_fg.png" BEST ALL
MAP bga_map3_0 "export/BG/3_0_fg.png" bga_tileset3_0 BEST BEST 0
TILESET bgb_tileset3_0 "export/BG/3_0_bg.png" BEST ALL
MAP bgb_map3_0 "export/BG/3_0_bg.png" bgb_tileset3_0 BEST BEST 0

PALETTE palette3_1 "export/BG/3_1_fg.png"
TILESET bga_tileset3_1 "export/BG/3_1_fg.png" BEST ALL
MAP bga_map3_1 "export/BG/3_1_fg.png" bga_tileset3_1 BEST BEST 0
TILESET bgb_tileset3_1 "export/BG/3_1_bg.png" BEST ALL
MAP bgb_map3_1 "export/BG/3_1_bg.png" bgb_tileset3_1 BEST BEST 0

PALETTE palette3_2 "export/BG/3_2_fg.png"
TILESET bga_tileset3_2 "export/BG/3_2_fg.png" BEST ALL
MAP bga_map3_2 "export/BG/3_2_fg.png" bga_tileset3_2 BEST BEST 0
TILESET bgb_tileset3_2 "export/BG/3_2_bg.png" BEST ALL
MAP bgb_map3_2 "export/BG/3_2_bg.png" bgb_tileset3_2 BEST BEST 0

PALETTE palette3_3 "export/BG/3_3_fg.png"
TILESET bga_tileset3_3 "export/BG/3_3_fg.png" BEST ALL
MAP bga_map3_3 "export/BG/3_3_fg.png" bga_tileset3_3 BEST BEST 0
TILESET bgb_tileset3_3 "export/BG/3_3_bg.png" BEST ALL
MAP bgb_map3_3 "export/BG/3_3_bg.png" bgb_tileset3_3 BEST BEST 0

PALETTE palette3_4 "export/BG/3_4_fg.png"
TILESET bga_tileset3_4 "export/BG/3_4_fg.png" BEST ALL
MAP bga_map3_4 "export/BG/3_4_fg.png" bga_tileset3_4 BEST BEST 0
TILESET bgb_tileset3_4 "export/BG/3_4_bg.png" BEST ALL
MAP bgb_map3_4 "export/BG/3_4_bg.png" bgb_tileset3_4 BEST BEST 0

//---------------------------------------------------------------------
// Stage3 Base
//---------------------------------------------------------------------
TILESET Stage3_base_tileset "export/OBJECT/Stage1_Base.png" BEST NONE

//---------------------------------------------------------------------
// Stage4
//---------------------------------------------------------------------
PALETTE palette4_0 "export/BG/4_0_fg.png"
TILESET bga_tileset4_0 "export/BG/4_0_fg.png" BEST ALL
MAP bga_map4_0 "export/BG/4_0_fg.png" bga_tileset4_0 BEST BEST 0
TILESET bgb_tileset4_0 "export/BG/4_0_bg.png" BEST ALL
MAP bgb_map4_0 "export/BG/4_0_bg.png" bgb_tileset4_0 BEST BEST 0

PALETTE palette4_1 "export/BG/4_1_fg.png"
TILESET bga_tileset4_1 "export/BG/4_1_fg.png" BEST ALL
MAP bga_map4_1 "export/BG/4_1_fg.png" bga_tileset4_1 BEST BEST 0
TILESET bgb_tileset4_1 "export/BG/4_1_bg.png" BEST ALL
MAP bgb_map4_1 "export/BG/4_1_bg.png" bgb_tileset4_1 BEST BEST 0

PALETTE palette4_2 "export/BG/4_2_fg.png"
TILESET bga_tileset4_2 "export/BG/4_2_fg.png" BEST ALL
MAP bga_map4_2 "export/BG/4_2_fg.png" bga_tileset4_2 BEST BEST 0
TILESET bgb_tileset4_2 "export/BG/4_2_bg.png" BEST ALL
MAP bgb_map4_2 "export/BG/4_2_bg.png" bgb_tileset4_2 BEST BEST 0

PALETTE palette4_3 "export/BG/4_3_fg.png"
TILESET bga_tileset4_3 "export/BG/4_3_fg.png" BEST ALL
MAP bga_map4_3 "export/BG/4_3_fg.png" bga_tileset4_3 BEST BEST 0
TILESET bgb_tileset4_3 "export/BG/4_3_bg.png" BEST ALL
MAP bgb_map4_3 "export/BG/4_3_bg.png" bgb_tileset4_3 BEST BEST 0

PALETTE palette4_4 "export/BG/4_4_fg.png"
TILESET bga_tileset4_4 "export/BG/4_4_fg.png" BEST ALL
MAP bga_map4_4 "export/BG/4_4_fg.png" bga_tileset4_4 BEST BEST 0
TILESET bgb_tileset4_4 "export/BG/4_4_bg.png" BEST ALL
MAP bgb_map4_4 "export/BG/4_4_bg.png" bgb_tileset4_4 BEST BEST 0

//---------------------------------------------------------------------
// Stage4 Base
//---------------------------------------------------------------------
TILESET Stage4_base_tileset "export/OBJECT/Stage1_Base.png" BEST NONE
