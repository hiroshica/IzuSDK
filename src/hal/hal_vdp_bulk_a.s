// [V1] L0 HAL: VDP bulk transfers (sgd::hal::vdp_bulk), see inc/hal/hal_vdp.h
// The VDP address (and auto increment) must be set before the call.
// Arguments are passed on the stack (GCC / SurugaCC m68k ABI), 32 bit slots.

#include "asm_mac.i"

// void hal_vdpbulk_fill16(u16 value, u16 num)
func hal_vdpbulk_fill16
    move.w  6(%sp),%d0                  // d0 = value
    move.w  10(%sp),%d1                 // d1 = num
    jeq     2f
    move.l  #0xC00000,%a0               // VDP data port
    subq.w  #1,%d1
1:
    move.w  %d0,(%a0)
    dbra    %d1,1b
2:
    rts

// void hal_vdpbulk_fill32(u32 value, u16 num)
func hal_vdpbulk_fill32
    move.l  4(%sp),%d0                  // d0 = value
    move.w  10(%sp),%d1                 // d1 = num
    jeq     2f
    move.l  #0xC00000,%a0
    subq.w  #1,%d1
1:
    move.l  %d0,(%a0)
    dbra    %d1,1b
2:
    rts

// void hal_vdpbulk_copy16(const u16* src, u16 num)
func hal_vdpbulk_copy16
    move.l  4(%sp),%a1                  // a1 = src
    move.w  10(%sp),%d1                 // d1 = num
    jeq     3f
    move.l  #0xC00000,%a0
    move.w  %d1,%d0
    lsr.w   #2,%d1                      // d1 = num / 4 (4 words per loop)
    jeq     2f
    subq.w  #1,%d1
1:
    move.l  (%a1)+,(%a0)
    move.l  (%a1)+,(%a0)
    dbra    %d1,1b
2:
    andi.w  #3,%d0                      // remaining words
    jeq     3f
    subq.w  #1,%d0
4:
    move.w  (%a1)+,(%a0)
    dbra    %d0,4b
3:
    rts

// void hal_vdpbulk_copy32(const u32* src, u16 num)
func hal_vdpbulk_copy32
    move.l  4(%sp),%a1                  // a1 = src
    move.w  10(%sp),%d1                 // d1 = num
    jeq     3f
    move.l  #0xC00000,%a0
    move.w  %d1,%d0
    lsr.w   #2,%d1                      // d1 = num / 4 (4 longs per loop)
    jeq     2f
    subq.w  #1,%d1
1:
    move.l  (%a1)+,(%a0)
    move.l  (%a1)+,(%a0)
    move.l  (%a1)+,(%a0)
    move.l  (%a1)+,(%a0)
    dbra    %d1,1b
2:
    andi.w  #3,%d0                      // remaining longs
    jeq     3f
    subq.w  #1,%d0
4:
    move.l  (%a1)+,(%a0)
    dbra    %d0,4b
3:
    rts

// void hal_vdpbulk_copy16_stride(const u16* src, u16 num, u16 inc)
// inc is in bytes (2 = contiguous)
func hal_vdpbulk_copy16_stride
    move.l  4(%sp),%a1                  // a1 = src
    move.w  10(%sp),%d1                 // d1 = num
    jeq     2f
    move.w  14(%sp),%d0                 // d0 = inc (bytes)
    ext.l   %d0
    move.l  #0xC00000,%a0
    subq.w  #1,%d1
1:
    move.w  (%a1),(%a0)
    adda.l  %d0,%a1
    dbra    %d1,1b
2:
    rts
