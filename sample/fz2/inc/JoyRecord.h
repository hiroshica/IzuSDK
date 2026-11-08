#ifndef _JOYRECORD_H_
#define _JOYRECORD_H_

#include <genesis.h>

typedef struct joy_record
{
    u16 m_Data;
    u16 m_Trig;
    u16 m_DataOld;
} Joy_Record;

extern Joy_Record m_JoyRecord[2];

void JoyInit();
void JoyRead();

#endif /* _JOYRECORD_H_ */

