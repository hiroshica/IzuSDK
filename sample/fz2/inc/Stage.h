#ifndef _STAGE_H_
#define _STAGE_H_

/*----------------------------------------------*/
#include <common.h>

#include <opaopa.h>
#include <BG.h>


void Stage_Init(Stage_Control *this);           // reset start init
void Stage_StartInit(Stage_Control *this);      // game start init
void Stage_PreUpdate(Stage_Control *this);
void Stage_Update(Stage_Control *this);
void Stage_PostUpdate(Stage_Control *this);
void Stage_DrawUpdate(Stage_Control *this);

#endif /* _STAGE_H_ */
