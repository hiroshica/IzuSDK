// [V1] L1 MW: VBlank scheduler (sgd::mw::sched), see inc/mw/sched.h
// SYS_doVBlankProcess() / SYS_doVBlankProcessEx() moved here from sys.c: they now run the registered
// tasks instead of calling DMA_flushQueue(), VDP_doVBlankScrollProcess(), PAL_doFadeStep() and JOY_update().

#include "config.h"
#include "types.h"

#include "sys.h"
#include "vdp.h"
#include "timer.h"
#include "tools.h"
#include "dma.h"

#include "hal/hal_vdp.h"
#include "mw/sched.h"


// user VBlank callback (sys.c)
extern VoidCallback *vblankCB;
extern vu16 VBlankProcess;
// frame load display (sys.c)
extern void SYS_doFrameLoadDisplay(void);

// VBlankProcess bit of each task slot (0 = always run)
static const u16 taskBits[SCHED_TASK_NUM] =
{
    PROCESS_DMA_TASK,
    PROCESS_VDP_SCROLL_TASK,
    PROCESS_PALETTE_FADING,
    0
};

static SchedTask* tasks[SCHED_TASK_NUM];
static VoidCallback* events[SCHED_EVENT_NUM];


void SCHED_reset(void)
{
    u16 i;

    for (i = 0; i < SCHED_TASK_NUM; i++) tasks[i] = NULL;
    for (i = 0; i < SCHED_EVENT_NUM; i++) events[i] = NULL;
}

void SCHED_setTask(u16 slot, SchedTask* task)
{
    if (slot < SCHED_TASK_NUM) tasks[slot] = task;
}

SchedTask* SCHED_getTask(u16 slot)
{
    if (slot < SCHED_TASK_NUM) return tasks[slot];
    return NULL;
}

void SCHED_setEventHandler(u16 event, VoidCallback* handler)
{
    if (event < SCHED_EVENT_NUM) events[event] = handler;
}

void SCHED_fireEvent(u16 event)
{
    if (event < SCHED_EVENT_NUM)
    {
        VoidCallback* cb = events[event];
        if (cb) cb();
    }
}


// run task of given slot if registered and its process bit is set, returns updated process bits
static u16 runTask(u16 slot, u16 vbp)
{
    SchedTask* task = tasks[slot];
    const u16 bit = taskBits[slot];

    if (task && (!bit || (vbp & bit)))
    {
        if (!task() && bit) vbp &= ~bit;
    }

    return vbp;
}

#if (LIB_LOG_LEVEL >= LOG_LEVEL_WARNING)
static void checkOverrun(u16 slot, u16 dmaSize)
{
    const u16 vcnt = hal_vdp_v_counter();

    // above scanline 2 ? better to warn about overrun
    if ((vcnt < 224) && (vcnt > 2))
    {
        if (slot == SCHED_TASK_DMA)
            KLog_U3("Warning: DMA task (", dmaSize, " bytes) completed outside VBlank area. Scanline after completion = ", vcnt, " on frame #", vtimer);
        else
            KLog_U3("Warning: VBlank task #", slot, " completed outside VBlank area. Scanline after completion = ", vcnt, " on frame #", vtimer);
    }
}
#endif


bool SYS_doVBlankProcess()
{
    return SYS_doVBlankProcessEx(ON_VBLANK_START);
}

NO_INLINE bool SYS_doVBlankProcessEx(VBlankProcessTime processTime)
{
    u16 slot;

    if (processTime != IMMEDIATELY)
    {
        // wait for VBlank
        if (VDP_waitVBlank(processTime == ON_VBLANK_START))
        {
            // frame late/miss detection and VBlank process forced on VBlank start ?
            if (processTime == ON_VBLANK_START)
            {
                // SYS_doVBlankProcess() was called from V-Int callback ?
                if (SYS_isInVInt())
                    // we need to return from interrupt as we don't have anyway to clear the new pending interrupt
                    // so we will take it again immediately but should be in time for this one :)
                    return FALSE;
            }
        }
    }

    u16 vbp = VBlankProcess;

    // DMA, scroll and palette tasks (in this order)
    for (slot = SCHED_TASK_DMA; slot <= SCHED_TASK_PALETTE; slot++)
    {
#if (LIB_LOG_LEVEL >= LOG_LEVEL_WARNING)
        const u16 dmaSize = (slot == SCHED_TASK_DMA)?DMA_getQueueTransferSize():0;
        const bool run = (tasks[slot] != NULL) && (vbp & taskBits[slot]);
#endif

        vbp = runTask(slot, vbp);

#if (LIB_LOG_LEVEL >= LOG_LEVEL_WARNING)
        if (run) checkOverrun(slot, dmaSize);
#endif
    }

    // store back
    VBlankProcess = vbp;

    // user VBlank callback
    (*vblankCB)();

    // frame load display
    SYS_doFrameLoadDisplay();

    // joy state refresh (and any 'always' task after the user callback)
    runTask(SCHED_TASK_JOY, 0);

    return TRUE;
}

bool SYS_nextFrame(void)
{
    return SYS_doVBlankProcess();
}
