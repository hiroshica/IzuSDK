// [V1] L0 HAL: Z80 bus / reset / bank / RAM access (sgd::hal::z80), see inc/hal/hal_z80.h
// Sound driver management (Z80_init, Z80_loadDriver.., bus protection) moved to src/mw/snd/z80_driver.c

#include "config.h"
#include "types.h"

#include "z80_ctrl.h"
#include "sys.h"
#include "tools.h"

#include "hal/hal_z80.h"


bool Z80_isBusTaken()
{
    return hal_z80_bus_taken()?TRUE:FALSE;
}

void Z80_requestBus(bool wait)
{
    vu16 *pw_bus;
    vu16 *pw_reset;

    // request bus (need to end reset)
    pw_bus = (u16 *) Z80_HALT_PORT;
    pw_reset = (u16 *) Z80_RESET_PORT;

    // take bus and end reset
    *pw_bus = 0x0100;
    *pw_reset = 0x0100;

    if (wait)
    {
        // wait for bus taken
        while (*pw_bus & 0x0100);
    }
}

bool Z80_getAndRequestBus(bool wait)
{
    vu16 *pw_bus;
    vu16 *pw_reset;

    pw_bus = (u16 *) Z80_HALT_PORT;

    // already requested ? just return TRUE
    if (!(*pw_bus & 0x0100)) return TRUE;

    pw_reset = (u16 *) Z80_RESET_PORT;

    // take bus and end reset
    *pw_bus = 0x0100;
    *pw_reset = 0x0100;

    if (wait)
    {
        // wait for bus taken
        while (*pw_bus & 0x0100);
    }

    return FALSE;
}

void Z80_releaseBus()
{
    hal_z80_release_bus();
}


void Z80_startReset()
{
    hal_z80_start_reset();
}

void Z80_endReset()
{
    hal_z80_end_reset();
}


void Z80_setBank(const u16 bank)
{
    vu8 *pb;
    u16 i, value;

    pb = (u8 *) Z80_BANK_REGISTER;

    i = 9;
    value = bank;
    while (i--)
    {
        *pb = value;
        value >>= 1;
    }
}

u8 Z80_read(const u16 addr)
{
    return ((vu8*) Z80_RAM)[addr];
}

void Z80_write(const u16 addr, const u8 value)
{
    ((vu8*) Z80_RAM)[addr] = value;
}


NO_INLINE void Z80_clear()
{
    SYS_disableInts();
    bool busTaken = Z80_getAndRequestBus(TRUE);

    const u8 zero = getZeroU8();
    vu8* dst = (u8*) Z80_RAM;
    u16 len = Z80_RAM_LEN;

    while(len--) *dst++ = zero;

    // release bus
    if (!busTaken) Z80_releaseBus();
    SYS_enableInts();
}

NO_INLINE void Z80_upload(const u16 to, const u8 *from, const u16 size)
{
    SYS_disableInts();
    bool busTaken = Z80_getAndRequestBus(TRUE);

    // copy data to Z80 RAM (need to use byte copy here)
    u8* src = (u8*) from;
    vu8* dst = (u8*) (Z80_RAM + to);
    u16 len = size;

    while(len--) *dst++ = *src++;

    // release bus
    if (!busTaken) Z80_releaseBus();
    SYS_enableInts();
}

NO_INLINE void Z80_download(const u16 from, u8 *to, const u16 size)
{
    SYS_disableInts();
    bool busTaken = Z80_getAndRequestBus(TRUE);

    // copy data from Z80 RAM (need to use byte copy here)
    vu8* src = (u8*) (Z80_RAM + from);
    u8* dst = (u8*) to;
    u16 len = size;

    while(len--) *dst++ = *src++;

    // release bus
    if (!busTaken) Z80_releaseBus();
    SYS_enableInts();
}
