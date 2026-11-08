/**
 *  \file mw/sched.h
 *  \brief VBlank scheduler (middleware)
 *
 * [V1] L1 MW (sgd::mw::sched). SYS_doVBlankProcess() runs the tasks registered here instead of
 * calling DMA / scroll / palette / joypad code directly, so the system unit no longer depends on
 * the modules above it (layer violations #1 and #10 of the V1 design).
 *
 * Default bindings are done once in sys.c (internal_reset), the "binding" place of the library.
 * A game can replace any task (SCHED_setTask) with its own implementation.
 *
 * Order of SYS_doVBlankProcess():
 *   DMA task -> scroll task -> palette task -> user VBlank callback -> frame load display -> joy task
 */

#ifndef _MW_SCHED_H_
#define _MW_SCHED_H_

#include "types.h"

/**
 *  \brief
 *      VBlank task. Return FALSE to clear the VBlankProcess bit of the slot (task done).
 */
typedef bool SchedTask(void);

#define SCHED_TASK_DMA              0   /**< run when PROCESS_DMA_TASK is set */
#define SCHED_TASK_SCROLL           1   /**< run when PROCESS_VDP_SCROLL_TASK is set */
#define SCHED_TASK_PALETTE          2   /**< run when PROCESS_PALETTE_FADING is set */
#define SCHED_TASK_JOY              3   /**< always run, after the user VBlank callback */
#define SCHED_TASK_NUM              4

/**
 *  \brief
 *      MW events: notifications between middleware modules without include dependency.
 */
#define SCHED_EVENT_VRAM_LAYOUT     0   /**< tilemap / table VRAM addresses changed (sprite engine defrags its VRAM) */
#define SCHED_EVENT_NUM             1

/**
 *  \brief
 *      Set (or remove with NULL) the task of a slot.
 */
void SCHED_setTask(u16 slot, SchedTask* task);
/**
 *  \brief
 *      Task of a slot (NULL if none).
 */
SchedTask* SCHED_getTask(u16 slot);

/**
 *  \brief
 *      Set (or remove with NULL) the handler of an event.
 */
void SCHED_setEventHandler(u16 event, VoidCallback* handler);
/**
 *  \brief
 *      Call the handler of an event (nothing if none).
 */
void SCHED_fireEvent(u16 event);

/**
 *  \brief
 *      Remove all tasks and event handlers (called on reset before the default bindings).
 */
void SCHED_reset(void);

#endif // _MW_SCHED_H_
