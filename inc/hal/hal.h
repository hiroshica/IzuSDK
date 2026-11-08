/**
 *  \file hal/hal.h
 *  \brief Hardware layer (HAL) umbrella header
 *
 * [V1] L0 HAL: the only layer that touches hardware registers. Everything is a macro except bulk
 * transfers / reset / interrupt mask (asm) so no function call is added on hot paths.
 * See doc/v1_layers.md.
 */

#ifndef _HAL_H_
#define _HAL_H_

#include "hal/hal_cpu.h"
#include "hal/hal_vdp.h"
#include "hal/hal_dma.h"
#include "hal/hal_z80.h"
#include "hal/hal_snd.h"
#include "hal/hal_io.h"
#include "hal/hal_mapper.h"
#include "hal/hal_timer.h"
#include "hal/hal_debug.h"

#endif // _HAL_H_
