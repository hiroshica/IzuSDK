// [V1] L0 HAL: VDP register shadow (sgd::hal::vdp), see inc/hal/hal_vdp.h
#include "config.h"
#include "types.h"

#include "hal/hal_vdp.h"

// shadow of VDP registers 0x00-0x12 (was 'regValues' in vdp.c)
u8 halVdpRegs[HAL_VDP_REG_NUM];
