/**
 *  \file hal/hal_snd.h
 *  \brief Sound chips hardware layer (HAL): YM2612 / PSG
 *
 * [V1] L0 HAL (sgd::hal::snd). Register writes with busy wait. The Z80 bus must be taken
 * by the caller for YM2612 access (use one bus request for a batch of writes, risk R6).
 */

#ifndef _HAL_SND_H_
#define _HAL_SND_H_

#include "types.h"

#define HAL_YM2612_PORT                     0xA04000
#define HAL_PSG_PORT                        0xC00011

/** \brief YM2612 status (bit 7 = busy) */
#define hal_ym_status()                     (*((vu8*) HAL_YM2612_PORT))
/** \brief Busy wait while YM2612 is busy */
#define hal_ym_wait()                       do { while (*((vs8*) HAL_YM2612_PORT) < 0); } while(0)
/** \brief Raw write on port 0-3 (no wait) */
#define hal_ym_write_port(port, v)          (((vu8*) HAL_YM2612_PORT)[(port) & 3] = (u8) (v))
/** \brief Register write: part 0/1, waits for busy before the address write */
#define hal_ym_write(part, reg, val)        do { hal_ym_wait(); hal_ym_write_port(((part) << 1) & 2, reg);   \
                                                 hal_ym_write_port((((part) << 1) & 2) + 1, val); } while(0)
/** \brief PSG write */
#define hal_psg_write(v)                    (*((vu8*) HAL_PSG_PORT) = (u8) (v))

#endif // _HAL_SND_H_
