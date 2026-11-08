/**
 *  \file hal/hal_timer.h
 *  \brief Time base hardware layer (HAL)
 *
 * [V1] L0 HAL (sgd::hal::timer). The time base is the V-Int counter (vtimer, incremented by the
 * V-Int vector) and the HV counter. Timer / sub-tick computations stay in src/mw/timer.c.
 */

#ifndef _HAL_TIMER_H_
#define _HAL_TIMER_H_

#include "types.h"
#include "hal/hal_vdp.h"

// V-Int counter (incremented by the V-Int vector, src/boot/sega.s)
extern vu32 vtimer;

/** \brief Frame counter (number of V-Int since reset) */
#define hal_timer_frames()                  (vtimer)
/** \brief HV counter */
#define hal_timer_hv()                      hal_vdp_hv_counter()
/** \brief Ticks (1/300 s) */
#define hal_timer_ticks()                   (hal_vdp_is_pal() ? (vtimer * 6) : (vtimer * 5))

#endif // _HAL_TIMER_H_
