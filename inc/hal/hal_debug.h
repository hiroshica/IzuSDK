/**
 *  \file hal/hal_debug.h
 *  \brief Emulator debug port hardware layer (HAL)
 *
 * [V1] L0 HAL (sgd::hal::debug). KMod / Blastem / Gens KMod debug output (src/hal/kdebug.s).
 * Calls compile to nothing when LIB_LOG_LEVEL is LOG_LEVEL_DISABLE (release builds).
 */

#ifndef _HAL_DEBUG_H_
#define _HAL_DEBUG_H_

#include "config.h"
#include "types.h"
#include "kdebug.h"

#if (LIB_LOG_LEVEL == LOG_LEVEL_DISABLE)
#define hal_debug_log(text)                 do {} while(0)
#define hal_debug_alert(text)               do {} while(0)
#else
#define hal_debug_log(text)                 KDebug_Alert(text)
#define hal_debug_alert(text)               KDebug_Alert(text)
#endif

#endif // _HAL_DEBUG_H_
