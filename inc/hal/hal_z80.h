/**
 *  \file hal/hal_z80.h
 *  \brief Z80 bus / reset / RAM hardware layer (HAL)
 *
 * [V1] L0 HAL (sgd::hal::z80). Bus request, reset line and Z80 RAM.
 * Function versions (Z80_requestBus(), Z80_upload(), ...) are in src/hal/z80_ctrl.c.
 * Sound driver loading / bus protection protocol are middleware (src/mw/snd/z80_driver.c).
 */

#ifndef _HAL_Z80_H_
#define _HAL_Z80_H_

#include "types.h"

#define HAL_Z80_HALT_PORT                   0xA11100
#define HAL_Z80_RESET_PORT                  0xA11200
#define HAL_Z80_RAM                         0xA00000
#define HAL_Z80_BANK_REGISTER               0xA06000

/** \brief TRUE if the 68000 owns the Z80 bus */
#define hal_z80_bus_taken()                 (!(*((vu16*) HAL_Z80_HALT_PORT) & 0x0100))
/** \brief Request the Z80 bus and end Z80 reset (no wait) */
#define hal_z80_request_bus_nowait()        (*((vu16*) HAL_Z80_HALT_PORT) = 0x0100, *((vu16*) HAL_Z80_RESET_PORT) = 0x0100)
/** \brief Request the Z80 bus and wait until granted */
#define hal_z80_request_bus()               do { hal_z80_request_bus_nowait(); while (*((vu16*) HAL_Z80_HALT_PORT) & 0x0100); } while(0)
/** \brief Release the Z80 bus */
#define hal_z80_release_bus()               (*((vu16*) HAL_Z80_HALT_PORT) = 0x0000)
/** \brief Assert Z80 reset */
#define hal_z80_start_reset()               (*((vu16*) HAL_Z80_RESET_PORT) = 0x0000)
/** \brief Release Z80 reset */
#define hal_z80_end_reset()                 (*((vu16*) HAL_Z80_RESET_PORT) = 0x0100)
/** \brief Z80 RAM byte (bus must be taken) */
#define hal_z80_ram(addr)                   (((vu8*) HAL_Z80_RAM)[addr])

#endif // _HAL_Z80_H_
