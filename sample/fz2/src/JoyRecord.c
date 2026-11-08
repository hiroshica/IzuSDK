
#include <JoyRecord.h>


Joy_Record m_JoyRecord[2];

u16 joy_index[2] = {
    JOY_1,
    JOY_2,
};

void JoyInit()
{
    for (int iI = 0; iI < 2; iI++)
    {
        m_JoyRecord[iI].m_Data = 0;
        m_JoyRecord[iI].m_Trig = 0;
        m_JoyRecord[iI].m_DataOld = 0;
    }
}

void JoyRead()
{
    for (int iI = 0; iI < 2; iI++)
    {
        Joy_Record *getrecord = &m_JoyRecord[iI];
        getrecord->m_DataOld = getrecord->m_Data;
        getrecord->m_Data = JOY_readJoypad(joy_index[iI]);
        getrecord->m_Trig = (getrecord->m_Data ^ getrecord->m_DataOld)&getrecord->m_Data;
    }
}

