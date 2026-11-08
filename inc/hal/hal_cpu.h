/**
 *  \file hal/hal_cpu.h
 *  \brief 68000 CPU hardware layer (HAL): interrupt mask, interrupt hooks
 *
 * [V1] L0 HAL (sgd::hal::cpu).
 * - interrupt mask: SYS_getInterruptMaskLevel() / SYS_setInterruptMaskLevel() / SYS_getAndSetInterruptMaskLevel()
 *   (src/hal/sys_a.s) and the hal_cpu_irq_xxx() macros below.
 * - interrupt hooks: the vectors (src/boot/sega.s) only call the hooks set here. The HAL never calls
 *   a module above it (rule D3): middleware registers its interrupt-time work with hal_cpu_set_vint_task().
 * - reset: SYS_reset() / SYS_hardReset() / SYS_assertReset() (src/hal/sys_a.s).
 */

#ifndef _HAL_CPU_H_
#define _HAL_CPU_H_

#include "types.h"

/**
 *  \brief
 *      H-Int trampoline: a JMP (xxx).L instruction patched in RAM (fast H-Int call).
 */
typedef union
{
    u8 code[6];
    struct
    {
        u16 jmpInst;
        VoidCallback* addr;
    };
} InterruptCaller;

// hooks called by the interrupt vectors (src/boot/sega.s)
extern VoidCallback *vintCB;            // user V-Int callback (SYS_setVIntCallback)
extern VoidCallback *vintTaskCB;        // library V-Int task (hal_cpu_set_vint_task), called before vintCB
extern InterruptCaller hintCaller;      // H-Int trampoline (SYS_setHIntCallback)
extern VoidCallback *eintCB;            // external interrupt (SYS_setExtIntCallback)
extern VoidCallback *intCB;             // other interrupts
extern vu16 intTrace;                   // bit 0 = in V-Int
extern u16 intLevelSave;

#define HAL_CPU_IN_VINT                 1

/**
 *  \brief
 *      Reset all interrupt hooks to empty handlers and the interrupt disable counter.
 */
void hal_cpu_init(void);

/**
 *  \brief
 *      Set the library V-Int task (called from the V-Int vector before the user V-Int callback).
 *      NULL removes it. Used by the bitmap engine (BMP_doVBlankProcess).
 */
void hal_cpu_set_vint_task(VoidCallback* cb);

/** \brief TRUE while in the V-Int handler */
#define hal_cpu_in_vint()               (intTrace & HAL_CPU_IN_VINT)

/** \brief Disable all interrupts (SR = 0x2700), no save */
#define hal_cpu_irq_disable()           __asm__ volatile ("move.w #0x2700,%%sr" ::: "cc")
/** \brief Enable interrupts down to level 3 (SR = 0x2300, ISDK default), no restore */
#define hal_cpu_irq_enable()            __asm__ volatile ("move.w #0x2300,%%sr" ::: "cc")

/** \brief Interrupt mask level (0-7), see SYS_getInterruptMaskLevel() */
#define hal_cpu_irq_mask_get()          SYS_getInterruptMaskLevel()
/** \brief Set interrupt mask level, see SYS_setInterruptMaskLevel() */
#define hal_cpu_irq_mask_set(v)         SYS_setInterruptMaskLevel(v)
/** \brief Set interrupt mask level and return the previous one, see SYS_getAndSetInterruptMaskLevel() */
#define hal_cpu_irq_mask_save_set(v)    SYS_getAndSetInterruptMaskLevel(v)

/** \brief Soft reset, see SYS_reset() */
#define hal_cpu_reset()                 SYS_reset()
/** \brief Hard reset, see SYS_hardReset() */
#define hal_cpu_hard_reset()            SYS_hardReset()

// implemented in src/hal/sys_a.s (also declared in sys.h)
u16 SYS_getInterruptMaskLevel(void);
void SYS_setInterruptMaskLevel(u16 value);
u16 SYS_getAndSetInterruptMaskLevel(u16 value);
void SYS_reset(void);
void SYS_hardReset(void);

#endif // _HAL_CPU_H_
