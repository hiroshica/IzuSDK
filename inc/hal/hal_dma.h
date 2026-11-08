/**
 *  \file hal/hal_dma.h
 *  \brief VDP DMA hardware layer (HAL)
 *
 * [V1] L0 HAL (sgd::hal::dma). DMA registers and DMA start, all macros.
 * The DMA queue (registration, capacity, temporary buffer) is middleware: dma.c (sgd::mw::dmaq).
 * The queue flush is the existing asm routine flushQueue() (src/hal/dma_a.s).
 */

#ifndef _HAL_DMA_H_
#define _HAL_DMA_H_

#include "types.h"
#include "hal/hal_vdp.h"

/** \brief DMA length registers (0x13 / 0x14), len in words */
#define hal_dma_set_len(len)                (hal_vdp_write_ctrl16(0x9300 + ((len) & 0xFF)), hal_vdp_write_ctrl16(0x9400 + (((len) >> 8) & 0xFF)))
/** \brief DMA 68000 source registers (0x15 - 0x17), src = 68000 address */
#define hal_dma_set_src(src)                (hal_vdp_write_ctrl16(0x9500 + ((((u32) (src)) >> 1) & 0xFF)),  \
                                             hal_vdp_write_ctrl16(0x9600 + ((((u32) (src)) >> 9) & 0xFF)),  \
                                             hal_vdp_write_ctrl16(0x9700 + ((((u32) (src)) >> 17) & 0x7F)))
/** \brief DMA source / length / auto increment registers for a 68000 -> VDP transfer (step = -1 keeps auto increment) */
#define hal_dma_load_regs(len, src, step)   do { if ((step) != -1) hal_vdp_set_autoinc(step); hal_dma_set_len(len); hal_dma_set_src(src); } while(0)

/**
 *  \brief
 *      Start the DMA with the given command (hal_vdp_cmd_xxx_dma(addr)).
 *      The second command word is written from memory (SEGA note: avoids DMA failure on some systems).
 */
#define hal_dma_start(cmd)                  do { vu32 _hal_dma_cmd[1]; vu16* _hal_dma_p = (vu16*) _hal_dma_cmd;   \
                                                 _hal_dma_cmd[0] = (cmd);                                        \
                                                 hal_vdp_write_ctrl16(_hal_dma_p[0]);                            \
                                                 hal_vdp_write_ctrl16(_hal_dma_p[1]); } while(0)
/**
 *  \brief
 *      First command word only (the caller writes the second one, e.g. after taking the Z80 bus).
 *      cmdp is a vu16* pointing to the 32 bit DMA command stored in memory.
 */
#define hal_dma_start_first(cmdp)           hal_vdp_write_ctrl16((cmdp)[0])
/** \brief Second command word (triggers the DMA), see hal_dma_start_first() */
#define hal_dma_start_second(cmdp)          hal_vdp_write_ctrl16((cmdp)[1])

/** \brief VRAM fill registers (len in bytes) + DMA fill mode */
#define hal_dma_set_fill(len)               (hal_dma_set_len(len), hal_vdp_write_ctrl16(0x9780))
/** \brief VRAM copy registers (from = VRAM address) + DMA copy mode */
#define hal_dma_set_copy(from, len)         (hal_dma_set_len(len), hal_vdp_write_ctrl16(0x9500 + ((from) & 0xFF)),  \
                                             hal_vdp_write_ctrl16(0x9600 + (((from) >> 8) & 0xFF)), hal_vdp_write_ctrl16(0x97C0))

/** \brief Busy wait for DMA completion */
#define hal_dma_wait()                      hal_vdp_wait_dma()

/** \brief Flush num queued DMA entries (asm, src/hal/dma_a.s; entries are the DMA queue of dma.c) */
#define hal_dma_flush_queue(num)            flushQueue(num)
void flushQueue(u16 num);

#endif // _HAL_DMA_H_
