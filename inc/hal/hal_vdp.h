/**
 *  \file hal/hal_vdp.h
 *  \brief VDP hardware layer (HAL): ports, status, command words, registers
 *
 * [V1] L0 HAL (sgd::hal::vdp). Everything here is a macro so no function call is generated,
 * whatever the compiler (SurugaCC does not inline functions, so a "static inline" HAL would cost
 * a JSR/RTS on each port access, see doc/v1_layers.md).
 *
 * The VDP_xxx_PORT / VDP_xxx_FLAG / VDP_xxx_ADDR / GET_xxx definitions come from vdp.h (same names,
 * vdp.h includes this file), the hal_vdp_xxx() macros are the HAL API of the V1 design.
 */

#ifndef _HAL_VDP_H_
#define _HAL_VDP_H_

#include "types.h"

/**
 *  \brief
 *      VDP Data port address.
 */
#define VDP_DATA_PORT           0xC00000
/**
 *  \brief
 *      VDP Control port address.
 */
#define VDP_CTRL_PORT           0xC00004
/**
 *  \brief
 *      VDP HV counter port address.
 */
#define VDP_HVCOUNTER_PORT      0xC00008

/**
 *  \deprecated
 *      Use #VDP_DATA_PORT instead
 */
#define GFX_DATA_PORT           _Pragma("GCC error \"This definition is deprecated, use VDP_DATA_PORT instead.\"")

/**
 *  \deprecated
 *      Use #VDP_CTRL_PORT instead
 */
#define GFX_CTRL_PORT           _Pragma("GCC error \"This definition is deprecated, use VDP_CTRL_PORT instead.\"")
/**
 *  \deprecated
 *      Use #VDP_HVCOUNTER_PORT instead
 */
#define GFX_HVCOUNTER_PORT      _Pragma("GCC error \"This definition is deprecated, use VDP_HVCOUNTER_PORT instead.\"")


/**
 *  \brief
 *      VDP FIFO empty flag.
 */
#define VDP_FIFOEMPTY_FLAG      (1 << 9)
/**
 *  \brief
 *      VDP FIFO full flag.
 */
#define VDP_FIFOFULL_FLAG       (1 << 8)
/**
 *  \brief
 *      VDP Vertical interrupt pending flag.
 */
#define VDP_VINTPENDING_FLAG    (1 << 7)
/**
 *  \brief
 *      VDP sprite overflow flag.
 */
#define VDP_SPROVERFLOW_FLAG    (1 << 6)
/**
 *  \brief
 *      VDP sprite collision flag.
 */
#define VDP_SPRCOLLISION_FLAG   (1 << 5)
/**
 *  \brief
 *      VDP odd frame flag.
 */
#define VDP_ODDFRAME_FLAG       (1 << 4)
/**
 *  \brief
 *      VDP Vertical blanking flag.
 */
#define VDP_VBLANK_FLAG         (1 << 3)
/**
 *  \brief
 *      VDP Horizontal blanking flag.
 */
#define VDP_HBLANK_FLAG         (1 << 2)
/**
 *  \brief
 *      VDP DMA busy flag.
 */
#define VDP_DMABUSY_FLAG        (1 << 1)
/**
 *  \brief
 *      VDP PAL mode flag.
 */
#define VDP_PALMODE_FLAG        (1 << 0)

/**
 *  \brief
 *      Set VDP command to read specified VRAM address.
 */
#define VDP_READ_VRAM_ADDR(adr)     (((0x0000 + ((adr) & 0x3FFF)) << 16) + (((adr) >> 14) | 0x00))
/**
 *  \brief
 *      Set VDP command to read specified CRAM address.
 */
#define VDP_READ_CRAM_ADDR(adr)     (((0x0000 + ((adr) & 0x7F)) << 16) + 0x20)
/**
 *  \brief
 *      Set VDP command to read specified VSRAM address.
 */
#define VDP_READ_VSRAM_ADDR(adr)    (((0x0000 + ((adr) & 0x7F)) << 16) + 0x10)

/**
 *  \brief
 *      Set VDP command to write at specified VRAM address.
 */
#define VDP_WRITE_VRAM_ADDR(adr)    (((0x4000 + ((adr) & 0x3FFF)) << 16) + (((adr) >> 14) | 0x00))
/**
 *  \brief
 *      Set VDP command to write at specified CRAM address.
 */
#define VDP_WRITE_CRAM_ADDR(adr)    (((0xC000 + ((adr) & 0x7F)) << 16) + 0x00)
/**
 *  \brief
 *      Set VDP command to write at specified VSRAM address.
 */
#define VDP_WRITE_VSRAM_ADDR(adr)   (((0x4000 + ((adr) & 0x7F)) << 16) + 0x10)

/**
 *  \brief
 *      Set VDP command to issue a DMA transfert to specified VRAM address.
 */
#define VDP_DMA_VRAM_ADDR(adr)      (((0x4000 + ((adr) & 0x3FFF)) << 16) + (((adr) >> 14) | 0x80))
/**
 *  \brief
 *      Set VDP command to issue a DMA transfert to specified CRAM address.
 */
#define VDP_DMA_CRAM_ADDR(adr)      (((0xC000 + ((adr) & 0x7F)) << 16) + 0x80)
/**
 *  \brief
 *      Set VDP command to issue a DMA transfert to specified VSRAM address.
 */
#define VDP_DMA_VSRAM_ADDR(adr)     (((0x4000 + ((adr) & 0x7F)) << 16) + 0x90)

/**
 *  \brief
 *      Set VDP command to issue a DMA VRAM copy to specified VRAM address.
 */
#define VDP_DMA_VRAMCOPY_ADDR(adr)  (((0x4000 + ((adr) & 0x3FFF)) << 16) + (((adr) >> 14) | 0xC0))

/**
 *  \brief
 *      Helper to write in vertical scroll table (same as VDP_WRITE_VSRAM_ADDR).
 */
#define VDP_VERT_SCROLL(adr)        VDP_WRITE_VSRAM_ADDR(adr)
/**
 *  \brief
 *      Helper to write in horizontal scroll table (same as VDP_WRITE_VRAM_ADDR(VDP_SCROLL_H + adr)).
 */
#define VDP_HORZ_SCROLL(adr)        VDP_WRITE_VRAM_ADDR(VDP_SCROLL_H + (adr))

/**
 *  \deprecated
 *      Use #VDP_READ_VRAM_ADDR instead
 */
#define GFX_READ_VRAM_ADDR(adr)     _Pragma("GCC error \"This definition is deprecated, use VDP_READ_VRAM_ADDR instead.\""))
/**
 *  \deprecated
 *      Use #VDP_READ_CRAM_ADDR instead
 */
#define GFX_READ_CRAM_ADDR(adr)     _Pragma("GCC error \"This definition is deprecated, use VDP_READ_CRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_READ_VSRAM_ADDR instead
 */
#define GFX_READ_VSRAM_ADDR(adr)    _Pragma("GCC error \"This definition is deprecated, use VDP_READ_VSRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_WRITE_VRAM_ADDR instead
 */
#define GFX_WRITE_VRAM_ADDR(adr)    _Pragma("GCC error \"This definition is deprecated, use VDP_WRITE_VRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_WRITE_CRAM_ADDR instead
 */
#define GFX_WRITE_CRAM_ADDR(adr)    _Pragma("GCC error \"This definition is deprecated, use VDP_WRITE_CRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_WRITE_VSRAM_ADDR instead
 */
#define GFX_WRITE_VSRAM_ADDR(adr)   _Pragma("GCC error \"This definition is deprecated, use VDP_WRITE_VSRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_DMA_VRAM_ADDR instead
 */
#define GFX_DMA_VRAM_ADDR(adr)      _Pragma("GCC error \"This definition is deprecated, use VDP_DMA_VRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_DMA_CRAM_ADDR instead
 */
#define GFX_DMA_CRAM_ADDR(adr)      _Pragma("GCC error \"This definition is deprecated, use VDP_DMA_CRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_DMA_VSRAM_ADDR instead
 */
#define GFX_DMA_VSRAM_ADDR(adr)     _Pragma("GCC error \"This definition is deprecated, use VDP_DMA_VSRAM_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_DMA_VRAMCOPY_ADDR instead
 */
#define GFX_DMA_VRAMCOPY_ADDR(adr)  _Pragma("GCC error \"This definition is deprecated, use VDP_DMA_VRAMCOPY_ADDR instead.\"")
/**
 *  \deprecated
 *      Use #VDP_VERT_SCROLL instead
 */
#define GFX_VERT_SCROLL(adr)        _Pragma("GCC error \"This definition is deprecated, use VDP_VERT_SCROLL instead.\"")
/**
 *  \deprecated
 *      Use #VDP_HORZ_SCROLL instead
 */
#define GFX_HORZ_SCROLL(adr)        _Pragma("GCC error \"This definition is deprecated, use VDP_HORZ_SCROLL instead.\"")


/**
 *  \brief
 *      Tests VDP status against specified flag (see VDP_XXX_FLAG).
 */
#define GET_VDP_STATUS(flag)         ((*(vu16*)(VDP_CTRL_PORT)) & (flag))
/**
 *  \brief
 *      Tests if current system is a PAL system (50 Hz).
 */
#define IS_PAL_SYSTEM                GET_VDP_STATUS(VDP_PALMODE_FLAG)

/**
 *  \brief
 *      Returns HV counter.
 */
#define GET_HVCOUNTER               (*(vu16*)(VDP_HVCOUNTER_PORT))
/**
 *  \brief
 *      Returns Horizontal counter.
 */
#define GET_HCOUNTER                (GET_HVCOUNTER & 0xFF)
/**
 *  \brief
 *      Returns Vertical counter.
 */
#define GET_VCOUNTER                (GET_HVCOUNTER >> 8)


// ---------------------------------------------------------------------------------------------
// HAL API (V1). All macros: 1 port access = 1 instruction, never a function call.
// ---------------------------------------------------------------------------------------------

/**
 *  \brief
 *      Number of VDP registers kept in the shadow table.
 */
#define HAL_VDP_REG_NUM                     0x13

/**
 *  \brief
 *      Shadow of the VDP registers 0x00-0x12 (written by hal_vdp_set_reg(..)).
 *      DMA queue entries write the DMA / auto increment registers directly, so the shadow of
 *      registers 0x0F and 0x13-0x17 is not reliable.
 */
extern u8 halVdpRegs[HAL_VDP_REG_NUM];

/** \brief Write a 16 bit word to the control port */
#define hal_vdp_write_ctrl16(v)             (*((vu16*) VDP_CTRL_PORT) = (u16) (v))
/** \brief Write a 32 bit command to the control port */
#define hal_vdp_write_ctrl32(cmd)           (*((vu32*) VDP_CTRL_PORT) = (u32) (cmd))
/** \brief Write a 16 bit word to the data port */
#define hal_vdp_write_data16(v)             (*((vu16*) VDP_DATA_PORT) = (u16) (v))
/** \brief Write a 32 bit long to the data port */
#define hal_vdp_write_data32(v)             (*((vu32*) VDP_DATA_PORT) = (u32) (v))
/** \brief Read a 16 bit word from the data port */
#define hal_vdp_read_data16()               (*((vu16*) VDP_DATA_PORT))
/** \brief Read a 32 bit long from the data port */
#define hal_vdp_read_data32()               (*((vu32*) VDP_DATA_PORT))

/** \brief Command words (compile time constant when addr is constant) */
#define hal_vdp_cmd_vram_r(addr)            VDP_READ_VRAM_ADDR(addr)
#define hal_vdp_cmd_vram_w(addr)            VDP_WRITE_VRAM_ADDR(addr)
#define hal_vdp_cmd_cram_r(addr)            VDP_READ_CRAM_ADDR(addr)
#define hal_vdp_cmd_cram_w(addr)            VDP_WRITE_CRAM_ADDR(addr)
#define hal_vdp_cmd_vsram_r(addr)           VDP_READ_VSRAM_ADDR(addr)
#define hal_vdp_cmd_vsram_w(addr)           VDP_WRITE_VSRAM_ADDR(addr)
#define hal_vdp_cmd_vram_dma(addr)          VDP_DMA_VRAM_ADDR(addr)
#define hal_vdp_cmd_cram_dma(addr)          VDP_DMA_CRAM_ADDR(addr)
#define hal_vdp_cmd_vsram_dma(addr)         VDP_DMA_VSRAM_ADDR(addr)
#define hal_vdp_cmd_vram_copy(addr)         VDP_DMA_VRAMCOPY_ADDR(addr)

/** \brief Register write command word */
#define hal_vdp_reg_cmd(reg, value)         (0x8000 | ((reg) << 8) | ((value) & 0xFF))
/** \brief Write a register without touching the shadow table */
#define hal_vdp_set_reg_raw(reg, value)     hal_vdp_write_ctrl16(hal_vdp_reg_cmd(reg, value))
/** \brief Write a register (0x00-0x12) and keep its shadow value */
#define hal_vdp_set_reg(reg, value)         (halVdpRegs[reg] = (u8) (value), hal_vdp_write_ctrl16(0x8000 | ((reg) << 8) | halVdpRegs[reg]))
/** \brief Re-send the shadow value of a register */
#define hal_vdp_flush_reg(reg)              hal_vdp_write_ctrl16(0x8000 | ((reg) << 8) | halVdpRegs[reg])
/** \brief Shadow value of a register (0x00-0x12) */
#define hal_vdp_get_regshadow(reg)          (halVdpRegs[reg])
/** \brief Set a bit mask in a register (0x00-0x12) */
#define hal_vdp_set_reg_bits(reg, mask)     hal_vdp_set_reg(reg, halVdpRegs[reg] | (mask))
/** \brief Clear a bit mask in a register (0x00-0x12) */
#define hal_vdp_clear_reg_bits(reg, mask)   hal_vdp_set_reg(reg, halVdpRegs[reg] & ~(mask))

/**
 *  \brief
 *      Set the auto increment register.
 *      Always written: DMA queue entries change it behind the shadow table so a "skip if same" test is not safe.
 */
#define hal_vdp_set_autoinc(n)              hal_vdp_set_reg(0x0F, n)

/** \brief Status register */
#define hal_vdp_status()                    (*((vu16*) VDP_CTRL_PORT))
/** \brief HV counter */
#define hal_vdp_hv_counter()                (*((vu16*) VDP_HVCOUNTER_PORT))
/** \brief V counter */
#define hal_vdp_v_counter()                 (hal_vdp_hv_counter() >> 8)
/** \brief H counter */
#define hal_vdp_h_counter()                 (hal_vdp_hv_counter() & 0xFF)
/** \brief TRUE on a PAL (50 Hz) system */
#define hal_vdp_is_pal()                    (hal_vdp_status() & VDP_PALMODE_FLAG)
/** \brief TRUE while in VBlank */
#define hal_vdp_in_vblank()                 (hal_vdp_status() & VDP_VBLANK_FLAG)

/** \brief Busy wait until VBlank area (returns immediately if already in VBlank) */
#define hal_vdp_wait_vblank()               do { while (!(hal_vdp_status() & VDP_VBLANK_FLAG)); } while(0)
/** \brief Busy wait until active area */
#define hal_vdp_wait_vactive()              do { while (hal_vdp_status() & VDP_VBLANK_FLAG); } while(0)
/** \brief Busy wait for DMA completion */
#define hal_vdp_wait_dma()                  do { while (hal_vdp_status() & VDP_DMABUSY_FLAG); } while(0)
/** \brief Busy wait until the VDP FIFO is empty */
#define hal_vdp_wait_fifo_empty()           do { while (!(hal_vdp_status() & VDP_FIFOEMPTY_FLAG)); } while(0)

/** \brief Enable / disable display */
#define hal_vdp_display_enable(on)          do { if (on) hal_vdp_set_reg_bits(0x01, 0x40); else hal_vdp_clear_reg_bits(0x01, 0x40); } while(0)
/** \brief Enable / disable V interrupt */
#define hal_vdp_int_enable_v(on)            do { if (on) hal_vdp_set_reg_bits(0x01, 0x20); else hal_vdp_clear_reg_bits(0x01, 0x20); } while(0)
/** \brief Enable / disable H interrupt */
#define hal_vdp_int_enable_h(on)            do { if (on) hal_vdp_set_reg_bits(0x00, 0x10); else hal_vdp_clear_reg_bits(0x00, 0x10); } while(0)
/** \brief Enable / disable external interrupt */
#define hal_vdp_int_enable_ext(on)          do { if (on) hal_vdp_set_reg_bits(0x0B, 0x08); else hal_vdp_clear_reg_bits(0x0B, 0x08); } while(0)
/** \brief Set H interrupt counter */
#define hal_vdp_set_hint_counter(v)         hal_vdp_set_reg(0x0A, v)


// ---------------------------------------------------------------------------------------------
// Bulk transfers (asm, src/hal/hal_vdp_bulk_a.s). The VDP address must be set before the call.
// ---------------------------------------------------------------------------------------------

/** \brief Write num words of value to the data port */
void hal_vdpbulk_fill16(u16 value, u16 num);
/** \brief Write num longs of value to the data port */
void hal_vdpbulk_fill32(u32 value, u16 num);
/** \brief Copy num words from src to the data port */
void hal_vdpbulk_copy16(const u16* src, u16 num);
/** \brief Copy num longs from src to the data port */
void hal_vdpbulk_copy32(const u32* src, u16 num);
/** \brief Copy num words from src to the data port, adding inc (bytes) to src after each word */
void hal_vdpbulk_copy16_stride(const u16* src, u16 num, u16 inc);

#endif // _HAL_VDP_H_
