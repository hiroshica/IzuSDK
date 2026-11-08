// [V1] L1 MW: Z80 sound driver management (sgd::mw::sound), moved from z80_ctrl.c
// Z80_init(), driver loading, Z80 V-Int callback and DMA bus protection protocol.
// Raw Z80 bus / RAM access stays in the HAL (src/hal/z80_ctrl.c).

#include "config.h"
#include "types.h"

#include "z80_ctrl.h"

#include "ym2612.h"
#include "psg.h"
#include "memory.h"
#include "timer.h"
#include "sys.h"
#include "tools.h"

#include "snd/sound.h"


// driver(s) flags
#define DRIVER_FLAG_DELAY_DMA    (1 << 0)


s16 currentDriver;
u16 driverFlags;
u16 busProtectSignalAddress;
__attribute__((externally_visible)) VoidCallback *z80VIntCB;


// Empty Callback
static void _empty_callback()
{
    //
}


NO_INLINE void Z80_init()
{
    // request Z80 bus
    Z80_requestBus(TRUE);
    // set bank to 0
    Z80_setBank(0);

    // no loaded driver
    currentDriver = -1;
    driverFlags = 0;
    busProtectSignalAddress = 0;
    z80VIntCB = _empty_callback;

    // load null/dummy driver as it's important to have Z80 active (state is preserved)
    SND_NULL_loadDriver();
}


s16 Z80_getLoadedDriver()
{
    return currentDriver;
}

void Z80_unloadDriver()
{
    // load NULL driver
    SND_NULL_loadDriver();
}

NO_INLINE void Z80_loadDriverInternal(const u8 *drv, u16 size)
{
    SYS_disableInts();
    Z80_requestBus(TRUE);

    // start by removing the Z80 vint task
    Z80_setVIntCallback(NULL);
    // remove Z80 bus protection (signal address set to 0)
    Z80_useBusProtection(0);
    // remove DMA delay
    Z80_setForceDelayDMA(FALSE);

    // reset sound chips
    YM2612_reset();
    PSG_reset();

    // clear z80 memory
    Z80_clear();
    // upload Z80 driver
    Z80_upload(0, drv, size);

    // reset Z80
    Z80_startReset();
    Z80_releaseBus();
    // wait a bit so Z80 reset completed
    waitSubTick(50);
    Z80_endReset();
    SYS_enableInts();
}

NO_INLINE void Z80_loadCustomDriver(const u8 *drv, u16 size)
{
    Z80_unloadDriver();
    Z80_loadDriverInternal(drv, size);

    // custom driver set
    currentDriver = Z80_DRIVER_CUSTOM;
}


bool Z80_isDriverReady()
{
    // point to Z80 status
    vu8* pb = (vu8*) Z80_DRV_STATUS;

    SYS_disableInts();
    // request Z80 BUS
    bool busTaken = Z80_getAndRequestBus(TRUE);

    // ready status
    bool ret = (*pb & Z80_DRV_STAT_READY)?TRUE:FALSE;

    if (!busTaken) Z80_releaseBus();
    SYS_enableInts();

    return ret;
}


VoidCallback* Z80_getVIntCallback(void)
{
    return z80VIntCB;
}

void Z80_setVIntCallback(VoidCallback *CB)
{
    if (CB) z80VIntCB = CB;
    else z80VIntCB = _empty_callback;
}

void Z80_useBusProtection(u16 signalAddress)
{
    busProtectSignalAddress = signalAddress;
}

void Z80_setBusProtection(bool value)
{
    // bus protection not defined ? --> exit
    if (!busProtectSignalAddress)
        return;

    // point to Z80 PROTECT parameter
    vu8* pb = (vu8*) (Z80_RAM + busProtectSignalAddress);

    SYS_disableInts();
    bool busTaken = Z80_getAndRequestBus(TRUE);

    *pb = value?1:0;

    // release bus
    if (!busTaken) Z80_releaseBus();
    SYS_enableInts();
}

void Z80_enableBusProtection()
{
    Z80_setBusProtection(TRUE);
}

void Z80_disableBusProtection()
{
    Z80_setBusProtection(FALSE);
}

bool Z80_getForceDelayDMA()
{
    return driverFlags & DRIVER_FLAG_DELAY_DMA;
}

void Z80_setForceDelayDMA(bool value)
{
    if (value) driverFlags |= DRIVER_FLAG_DELAY_DMA;
    else driverFlags &= ~DRIVER_FLAG_DELAY_DMA;
}
