
#define __GLOBALWORK__
/*----------------------------------------------*/
#include <common.h>
#include <Stage.h>

extern SpriteDefinition sp1x1;

//------------------------------------------------
//------------------------------------------------
void GlobalWork_Init(void)
{
    //VRAM = TILE_USER_INDEX;
    //BG_STARTVRAM = TILE_USER_INDEX;
    //OBJ_STARTVRAM = TILE_USER_INDEX;
    m_JobMode = JOB_NONE;
}

//------------------------------------------------
//------------------------------------------------
Vect2D_s32 Stage_CalcScreen(Stage_Control *this, Vect2D_ff32 *inpos)
{
    Vect2D_s32 retdata;
    retdata.x = FF32_toRoundedInt(inpos->x - this->m_ViewPort.x) + SCREEN_CETERX;
    retdata.y = FF32_toRoundedInt(inpos->y - this->m_ViewPort.y) + SCREEN_CETERY;
    //kprintf("calc X = %d", (int)retdata.x);
    return retdata;
}
//------------------------------------------------
//------------------------------------------------
void SpriteInit(){
    // スプライトエンジンの初期化
#if 0
    // 旧コード: SPR_init() は SGDK スプライトVRAMを 420 タイル確保する。
    // BG-A=0xA000 の配置だとユーザー領域が 748 タイルしか残らず、BG(512)+固定スプライト分が入らない
    SPR_init();
#else
    // 修正: 自動VRAM確保用の領域は SPR_ENGINE_VRAM_SIZE 分だけ予約 (固定スプライトは region_spritesFixed で手動確保)
    SPR_initEx(SPR_ENGINE_VRAM_SIZE);
#endif
    for (int iI = 0; iI < SPRITE_BUFFER; ++iI)
    {
        m_Sprite[iI] = SPR_addSpriteExSafe(&sp1x1, 0, 24, TILE_ATTR_FULL(PAL0, TRUE, FALSE, FALSE, 0), 0);
        SPR_setVisibility(m_Sprite[iI], HIDDEN);
        m_Sprite[iI]->data = 0;
    }
}
//------------------------------------------------
//------------------------------------------------
Sprite *GetSpriteBuffer()
{
    Sprite *spr = NULL;
    for (int iI = 0; iI < SPRITE_BUFFER; ++iI){
        if( !((m_Sprite[iI]->data)&SPRITE_WORK_USE) ) {
            spr = m_Sprite[iI];
            spr->data |= SPRITE_WORK_USE;
            break;
        }
    }
    return spr;
}
//------------------------------------------------
//------------------------------------------------
void ReleaseSpriteBuffer(Sprite *inspr)
{
    inspr->data = 0;
    //inspr->spr_mode = SPR_DEFAULT;
    SPR_setVisibility(inspr, HIDDEN);
}
