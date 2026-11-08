// [V1] L0 HAL: CPU interrupt hooks and interrupt disable counter (sgd::hal::cpu), see inc/hal/hal_cpu.h
// Moved from sys.c. The interrupt vectors (src/boot/sega.s) only call the hooks defined here.

#include "config.h"
#include "types.h"

#include "sys.h"
#include "tools.h"

#include "hal/hal_cpu.h"


// user V-Int, H-Int, Ext-Int and Int callbacks
__attribute__((externally_visible)) VoidCallback *vintCB;
__attribute__((externally_visible)) VoidCallback *vintTaskCB;
__attribute__((externally_visible)) InterruptCaller hintCaller;
__attribute__((externally_visible)) VoidCallback *eintCB;
__attribute__((externally_visible)) VoidCallback *intCB;

__attribute__((externally_visible)) vu16 intTrace;

// need to be accessed from external
u16 intLevelSave;
static s16 disableIntStack;


// Empty Callback
static void _empty_callback()
{
    //
}

// Empty h-int Callback
static HINTERRUPT_CALLBACK _empty_hint_callback()
{
    //
}


void hal_cpu_init(void)
{
    vintCB = _empty_callback;
    vintTaskCB = _empty_callback;
    // fast hint call (auto modified JMP instruction)
    hintCaller.jmpInst = 0x4EF9;                // JMP (xxx).L
    hintCaller.addr = _empty_hint_callback;
    eintCB = _empty_callback;
    intTrace = 0;
    intLevelSave = 0;
    disableIntStack = 0;
}

void hal_cpu_set_vint_task(VoidCallback* cb)
{
    if (cb) vintTaskCB = cb;
    else vintTaskCB = _empty_callback;
}

void SYS_disableInts()
{
    // in interrupt --> return
    if (intTrace != 0)
    {
#if (LIB_LOG_LEVEL >= LOG_LEVEL_WARNING)
        // KDebug_Alert("SYS_disableInts() warning: call during interrupt (ignored)");
#endif

        return;
    }

    // disable interrupts
    if (disableIntStack++ == 0)
        intLevelSave = SYS_getAndSetInterruptMaskLevel(7);
#if (LIB_LOG_LEVEL >= LOG_LEVEL_WARNING)
    else
    {
        if (disableIntStack <= 0)
            KLog_S1_("SYS_disableInts() fails: need ", (-disableIntStack) + 1, " more");
    #if (LIB_LOG_LEVEL >= LOG_LEVEL_INFO)
        else
            KLog_S1("SYS_disableInts() info: inner call = ", disableIntStack);
    #endif
    }
#endif
}

void SYS_enableInts()
{
    // in interrupt --> return
    if (intTrace != 0)
    {
#if (LIB_LOG_LEVEL >= LOG_LEVEL_WARNING)
        // KDebug_Alert("SYS_enableInts() fails: call during interrupt");
#endif

        return;
    }

    // reenable interrupts
    if (--disableIntStack == 0)
        SYS_setInterruptMaskLevel(intLevelSave);
#if (LIB_LOG_LEVEL >= LOG_LEVEL_WARNING)
    else
    {
        if (disableIntStack < 0)
            KLog_S1("SYS_enableInts() fails: already enabled = ", disableIntStack);
    #if (LIB_LOG_LEVEL >= LOG_LEVEL_INFO)
        else
            KLog_S1_("SYS_enableInts() info: inner call, need ", disableIntStack, " more");
    #endif
    }
#endif
}

void SYS_setVIntCallback(VoidCallback *CB)
{
    if (CB) vintCB = CB;
    else vintCB = _empty_callback;
}

void SYS_setHIntCallback(VoidCallback *CB)
{
    if (CB) hintCaller.addr = CB;
    else hintCaller.addr = _empty_hint_callback;
}

void SYS_setExtIntCallback(VoidCallback *CB)
{
    if (CB) eintCB = CB;
    else eintCB = _empty_callback;
}

bool SYS_isInVInt()
{
    return (intTrace & HAL_CPU_IN_VINT)?TRUE:FALSE;
}
