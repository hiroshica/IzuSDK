#ifndef _GLOBALWORK_H_
#define _GLOBALWORK_H_

//GLOBAL Map *NowMapA;
//GLOBAL Map *NowMapB;
//GLOBAL s16 NowStageNo;
//GLOBAL s16 NowAreaNo;

GLOBAL u16 PaletteWork[4][0x10];

GLOBAL char mesgbbuf[41];

//GLOBAL s16 VRAM;
//GLOBAL s16 BG_STARTVRAM;
//GLOBAL s16 OBJ_STARTVRAM;
GLOBAL int16_t m_JobMode;

GLOBAL Stage_Control m_Stage_Control;

GLOBAL Sprite *m_Sprite[SPRITE_BUFFER];

void GlobalWork_Init(void);

Vect2D_s32 Stage_CalcScreen(Stage_Control *this, Vect2D_ff32 *inpos);
void SpriteInit();
Sprite *GetSpriteBuffer();
void ReleaseSpriteBuffer(Sprite *inspr);

// SpriteTables.c
// 基地の手打ちテーブル base_spr は SPRJ (res_gfx.res の stage1_base) に置き換えた



#endif /* _GLOBALWORK_H_ */

