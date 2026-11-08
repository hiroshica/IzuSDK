// [V1] L1 MW: sprite engine port (port::sprite), see inc/mw/sprite_port.h
// SPR_update() is the only runtime replaceable entry (S3 boundary, one indirect call per frame).

#include "config.h"
#include "types.h"

#include "mw/sprite_port.h"

const SpriteEnginePort sprEnginePortDefault =
{
    "default+SPRX",
    SPR_updateDefault
};

static const SpriteEnginePort* port = &sprEnginePortDefault;
// update function of the current port (copied so SPR_update() costs a single indirect call)
static VoidCallback* updateFn = SPR_updateDefault;


void SPR_setEnginePort(const SpriteEnginePort* p)
{
    if (p && p->update) port = p;
    else port = &sprEnginePortDefault;
    updateFn = port->update;
}

const SpriteEnginePort* SPR_getEnginePort(void)
{
    return port;
}

void SPR_update(void)
{
    updateFn();
}

