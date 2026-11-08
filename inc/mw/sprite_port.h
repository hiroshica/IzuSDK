/**
 *  \file mw/sprite_port.h
 *  \brief Sprite engine port (runtime replaceable update)
 *
 * [V1] port::sprite of the V1 design. The sprite engine API (SPR_xxx / SPRX_xxx) is statically bound
 * (no cost); only SPR_update(), called once per frame, goes through this port so a game can switch to
 * another sprite engine at run time (cost: one indirect call per frame, about 16 cycles).
 *
 * The default port is the built-in engine with SPRX (extended frames) support: SPR_updateDefault().
 * Not available with LEGACY_SPRITE_ENGINE = 1 (the legacy engine has its own SPR_update()).
 */

#ifndef _MW_SPRITE_PORT_H_
#define _MW_SPRITE_PORT_H_

#include "config.h"
#include "types.h"

#if (LEGACY_SPRITE_ENGINE == 0)

/**
 *  \brief
 *      Sprite engine port.
 *
 *  \param name
 *      engine name (for debug)
 *  \param update
 *      called by SPR_update()
 */
typedef struct
{
    const char* name;
    VoidCallback* update;
} SpriteEnginePort;

/**
 *  \brief
 *      Default port: built-in sprite engine with SPRX support (update = SPR_updateDefault).
 */
extern const SpriteEnginePort sprEnginePortDefault;

/**
 *  \brief
 *      Update of the built-in sprite engine (what SPR_update() did before the port).
 *      A custom port can call it to chain the built-in engine.
 */
void SPR_updateDefault(void);

/**
 *  \brief
 *      Set the sprite engine port used by SPR_update(). NULL restores the default port.
 */
void SPR_setEnginePort(const SpriteEnginePort* port);
/**
 *  \brief
 *      Current sprite engine port.
 */
const SpriteEnginePort* SPR_getEnginePort(void);

#endif // LEGACY_SPRITE_ENGINE

#endif // _MW_SPRITE_PORT_H_
