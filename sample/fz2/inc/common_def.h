#ifndef _COMMON_DEF_H_
#define _COMMON_DEF_H_

// clang-format off
//------------------------------------------------
#define SCREEN_WIDTH       (320)
#define SCREEN_HEIGHT      (224)
#define SCREEN_HWIDTH      (SCREEN_WIDTH/2)
#define SCREEN_CETERX      (SCREEN_WIDTH/2)
#define SCREEN_CETERY      (SCREEN_HEIGHT/2)
#define SCREEN_CALC_LEFT   (FASTFIX32(0))
#define SCREEN_CALC_CENTER (FASTFIX32(SCREEN_CETERX))
#define SCREEN_CALC_RIGHT  (FASTFIX32(320))
enum {
    LIMIT_NONE,
    LEFI_LIMIT_OVER,
    RIGHT_LIMIT_OVER,
};
/*----------------------------------------------*/
#if 0
// 旧定義: TILE_MAX_NUM は VDP のマップアドレス設定で実行時に値が変わる (0xC000時=1536, 0xA000時=1280)。
// Stage_Init で SetupVRAM より前に評価されていたため、region_spritesFixed が
// SGDKスプライトVRAM/フォント/BG-Aネームテーブルと重なる位置に作られていた。
#define BG_TILE_MAX      (TILE_MAX_NUM/2)
#define SPR_TILE_MAX     (TILE_MAX_NUM - BG_TILE_MAX)
#endif
// region_BG のタイル数 (BGInit で 512 タイル確保して使う)
#define BG_REGION_SIZE       (512)
// SGDK スプライトエンジンの自動VRAM確保(SPR_addSprite / SPR_FLAG_AUTO_VRAM_ALLOC)用に予約するタイル数
// 全スプライトを手動確保で運用しているので小さめにしている。自動確保を使う場合はここを増やす
#define SPR_ENGINE_VRAM_SIZE (64)
/*----------------------------------------------*/
#define SPR_LEFT_LIMIT    (0)
#define SPR_RIGHT_LIMIT   (SCREEN_WIDTH - 24)
#define eUpLine       (24)
#define eTouchLine    (SCREEN_HEIGHT - 24)
#define eEnginesMax   (6)

/*----------------------------------------------*/
#define OPAOPA_LR_OFFSET1  (40)
#define OPAOPA_LR_OFFSET2  (80)
#define OPAOPA_LIMIT_L1     ((SCREEN_CETERX-OPAOPA_LR_OFFSET1))     // scroll left rimit
#define OPAOPA_LIMIT_R1     ((SCREEN_CETERX+OPAOPA_LR_OFFSET1))     // scroll right limit
#define OPAOPA_LIMIT_L2     ((SCREEN_CETERX-OPAOPA_LR_OFFSET2))     // scroll left rimit
#define OPAOPA_LIMIT_R2     ((SCREEN_CETERX+OPAOPA_LR_OFFSET2))     // scroll right limit
/*----------------------------------------------*/
//#define WORLD_LIMIT_X      (FASTFIX32(SCREEN_WIDTH*3))
//#define WORLD_LIMIT_Y      (FASTFIX32(SCREEN_HEIGHT*3))
#define VIEWPORT_MOVEX1    (0.5)
#define VIEWPORT_MOVEX2    (1)
/*----------------------------------------------*/
#define SPRITE_BUFFER         (10)//(70)                // Spriteの最大サイズ
// dataフィールド内訳
#define SPRITE_WORK_USE         (1<<0)

/*----------------------------------------------*/
enum
{
    JOB_NONE = 0,
    JOB_INIT,
    JOB_STAGEINIT,
    JOB_FADE_IN_START,
    JOB_FADE_IN,
    JOB_IDLE,
    JOB_FADE_OUT,
    JOB_MAX
};
/*----------------------------------------------*/
// opaopa.c/h
/*----------------------------------------------*/
enum LR_MODE{
    NONE_CTRL,
    LEFT_CTRL,
    RIGHT_CTRL
};
/*----------------------------------------------*/
typedef struct _StageStartData
{
    s16 startArea;
    s16 startbaseno;
} StageStartData;

/*----------------------------------------------*/
typedef struct _Opaopa_Control
{
    Sprite *m_spr;
    Vect2D_ff32 m_Pos;
    Vect2D_ff32 m_OldPos;
    Vect2D_ff32 m_AddPos;
    Vect2D_ff32 m_BaseAddPos;


    int  m_NoFlipFlag;  // LR_MODE
    s16 m_AnimIndex;
    s16 m_Engines;
    bool m_FastAnim;
    bool m_TouchLine;
    bool m_ResetXPos;
} Opaopa_Control;

/*----------------------------------------------*/
#define STAGE_MAX       (8)
#define BG_AREA_MAX     (5)
#define BG_BASE_MAX     (5)
#define SCRXB_DIV        FASTFIX32(0.5)
#define SCRYB_DIV        FASTFIX32(1)
#define SCRYA_OFFSET     (8 * 0)
#define SCRYB_OFFSET     (8 * 0)

enum BG_Mode {
    BG_Mode_None,
    BG_Mode_Inactive,


    BG_Mode_Active,
    BG_Mode_Warp_Area,
    BG_Mode_Warp_Boss,
};

typedef struct _BGBase
{
    ff32   m_X;
    ff32   m_Y;
    Sprite *m_Spr;
    s16 m_JobMode;          // 現在の状況 (BG_Modeを使用)
    s16 m_SystemMode;       // この基地の死んだときのモード (BG_Modeを使用)
    s16 m_Life;
    s16 m_WarpArea;
    s16 m_WarpBaseNo;
} BGBase;

typedef struct bg_record
{
    const Palette       *pal;
    const TileSet       *tileA;
    const MapDefinition *mapA;
    const TileSet       *tileB;
    const MapDefinition *mapB;
    s32                 areaWidth;
    s16                 baseSize;
    BGBase              *m_BGBaseInitData;
} Area_Record;

typedef struct _Stage_Record
{
    const SprxInfo     *m_StageBaseData;    // 基地のスプライト (SPRJ)  旧: const TileSet *
    s16                m_AreaSize;
    Area_Record        m_Area[];
} Stage_Record;

typedef struct _BG_Control
{
    u16 VRAM;
    Map *NowMapA;
    Map *NowMapB;
    //s16 PosAX, PosAY;
    //s16 PosBX, PosBY;
    Vect2D_ff32 m_PosA;
    Vect2D_ff32 m_PosB;
    Stage_Record *StageRecord;
    s16    m_NowArea;
    s16    m_NowFrame;
    s32    m_AreaWidth;
    BGBase m_BGBase[BG_AREA_MAX][BG_BASE_MAX];
} BG_Control;

/*----------------------------------------------*/
enum ViewAddMode {
    VM_None,
    VM_L_Mode0,
    VM_L_Mode1,
    VM_L_Mode2,
    VM_R_Mode0,
    VM_R_Mode1,
    VM_R_Mode2,
};

typedef struct
{
    s16  m_StageNo;
    s16  m_AreaNo;
    Vect2D_ff32 m_StartPos;      // Area開始時の位置情報
    Vect2D_ff32 m_MoveVec;
    Vect2D_ff32 m_OldViewPort;      // View Center
    Vect2D_ff32 m_ViewPort;      // View Center
    Vect2D_ff32 m_AddViewPort;
    s16 m_OldViewAddMode;
    s16 m_ViewAddMode;
    bool m_StartFlag;

    VRAMRegion region_BG;
    VRAMRegion region_spritesFixed;

    Opaopa_Control m_Opaopa_Control;
    BG_Control m_NowBG;
} Stage_Control;


// clang-format on
#endif /* _COMMON_DEF_H_ */

