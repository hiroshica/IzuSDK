#ifndef _BG_H_
#define _BG_H_

#include <common.h>

void BGInit(BG_Control *this, u16 vram);
void BGStartInit(BG_Control *this, s16 stageno);
void BG_PreUpdate(BG_Control *this);
void BG_DrawUpdate(BG_Control *this, Stage_Control *mother, ff32 xain);

BGBase *BGGetBaseData(BG_Control *this,s16 areano,s16 baseno);
void BGSetupArea(BG_Control *this, s16 areano);
s16 GetNowAreaSize(BG_Control *this);
s16 GetStageAreaWidth(BG_Control *this);

#endif /* _BG_H_ */

