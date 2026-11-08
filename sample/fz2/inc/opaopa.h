#ifndef _OPAOPA_H_
#define _OPAOPA_H_

#include <common.h>

void OPAOPA_StartInit(Opaopa_Control *this, Vect2D_ff32 *spos, Sprite *inSpr);
void OPAOPA_PreUpdate(Opaopa_Control *this, Stage_Control *mother, bool xf, bool yf);
void OPAOPA_Update(Opaopa_Control *this);
void OPAOPA_PostUpdate(Opaopa_Control *this);
void OPAOPA_DrawUpdate(Opaopa_Control *this, Stage_Control *mother);


#endif /* _OPAOPA_H_ */
