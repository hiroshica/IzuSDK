/**
 *  \file hal/hal_mapper.h
 *  \brief Cartridge mapper hardware layer (HAL): SSF2 bank registers, SRAM control
 *
 * [V1] L0 HAL (sgd::hal::mapper). Function versions: src/hal/mapper.c, src/hal/sram.c, src/hal/sram_a.s.
 */

#ifndef _HAL_MAPPER_H_
#define _HAL_MAPPER_H_

#include "types.h"

#define HAL_MAPPER_BASE                     0xA130F1
#define HAL_SRAM_CONTROL                    0xA130F1

/** \brief Set SSF2 bank register (regionIndex 1-7) to bank */
#define hal_mapper_set_bank(regionIndex, bank)  (((vu8*) HAL_MAPPER_BASE)[(regionIndex) * 2] = (u8) (bank))
/** \brief SRAM control: 0 = disabled (ROM), 1 = enabled read / write, 3 = read only */
#define hal_sram_control(v)                 (*((vu8*) HAL_SRAM_CONTROL) = (u8) (v))

#endif // _HAL_MAPPER_H_
