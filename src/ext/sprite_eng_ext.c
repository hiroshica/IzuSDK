// [EXT] SPRX : fork-only extension (not part of upstream SGDK), see inc/ext/sprite_eng_ext.h
#include "config.h"
#include "types.h"

#if (MODULE_SPRITE_EXT != 0)

#include "ext/sprite_eng_ext.h"
#include "sprite_eng.h"
#include "vdp_tile.h"
#include "vram.h"


s16 SPRX_loadResident(const SprxInfo* info, VRAMRegion* region, TransferMethod tm)
{
    if ((info->vramMode != SPRX_RESIDENT) || (info->residentTiles == NULL)) return -1;

    const s16 ind = VRAM_alloc(region, info->vramTiles);
    if (ind < 0) return -1;

    if (!VDP_loadTileSet(info->residentTiles, ind, tm))
    {
        VRAM_free(region, ind);
        return -1;
    }

    return ind;
}

bool SPRX_loadResidentAt(const SprxInfo* info, u16 index, TransferMethod tm)
{
    if ((info->vramMode != SPRX_RESIDENT) || (info->residentTiles == NULL)) return FALSE;

    return VDP_loadTileSet(info->residentTiles, index, tm);
}

static u16 getFlag(const SprxInfo* info)
{
    u16 flag = SPR_FLAG_EXT_FRAME | SPR_FLAG_AUTO_VISIBILITY;

    if (info->vramMode == SPRX_TRANSFER) flag |= SPR_FLAG_AUTO_TILE_UPLOAD;

    return flag;
}

Sprite* SPRX_addSprite(const SprxInfo* info, s16 x, s16 y, u16 attribut, u16 vramIndex, u16 flag)
{
    // never use engine auto VRAM allocation (VRAM is managed by the caller)
    flag = (flag & ~SPR_FLAG_AUTO_VRAM_ALLOC) | getFlag(info);

    return SPR_addSpriteEx(info->def, x - info->pivotX, y - info->pivotY,
                           (attribut & TILE_ATTR_MASK) | (vramIndex & TILE_INDEX_MASK), flag);
}

bool SPRX_setDefinition(Sprite* sprite, const SprxInfo* info, u16 vramIndex)
{
    u16 status = sprite->status;

    // set extended frame mode *before* the definition change (frame update happens on next SPR_update)
    status |= SPR_FLAG_EXT_FRAME;
    if (info->vramMode == SPRX_TRANSFER) status |= SPR_FLAG_AUTO_TILE_UPLOAD;
    else status &= ~SPR_FLAG_AUTO_TILE_UPLOAD;
    sprite->status = status;

    // fixed VRAM index first (also disables auto VRAM allocation)
    if (!SPR_setVRAMTileIndex(sprite, vramIndex)) return FALSE;

    return SPR_setDefinition(sprite, info->def);
}

void SPRX_detach(Sprite* sprite)
{
    sprite->status &= ~SPR_FLAG_EXT_FRAME;
}

void SPRX_setPosition(Sprite* sprite, const SprxInfo* info, s16 x, s16 y)
{
    SPR_setPosition(sprite, x - info->pivotX, y - info->pivotY);
}

const SprxBoxList* SPRX_getBoxes(const Sprite* sprite)
{
    const AnimationFrame* frame = sprite->frame;

    if (frame == NULL) return NULL;

    return (const SprxBoxList*) frame->collision;
}

bool SPRX_getBox(const Sprite* sprite, u16 kind, u16 index, SprxRect* out)
{
    const SprxBoxList* list = SPRX_getBoxes(sprite);

    if (list == NULL) return FALSE;

    const SprxBox* box = list->boxes;
    u16 n = list->numBox;

    while(n--)
    {
        if ((box->kind == kind) && (box->index == index))
        {
            const u16 attr = sprite->attribut;
            s16 x = box->x;
            s16 y = box->y;
            const s16 w = box->w;
            const s16 h = box->h;

            // bounding box is symmetric around pivot (done by converter) so we flip around pivot
            if (attr & TILE_ATTR_HFLIP_MASK) x = -x - w;
            if (attr & TILE_ATTR_VFLIP_MASK) y = -y - h;

            out->x = x;
            out->y = y;
            out->w = w;
            out->h = h;

            return TRUE;
        }

        // sorted by kind
        if (box->kind > kind) break;
        box++;
    }

    return FALSE;
}

VDPSprite* SPRX_fillVDPSprites(const Sprite* sprite, VDPSprite* vdpSprite, u8* vdpSpriteInd)
{
    static const u16 visibilityMask[17] =
    {
        0x0000, 0x8000, 0xC000, 0xE000, 0xF000, 0xF800, 0xFC00, 0xFE00,
        0xFF00, 0xFF80, 0xFFC0, 0xFFE0, 0xFFF0, 0xFFF8, 0xFFFC, 0xFFFE,
        0xFFFF
    };

    const AnimationFrame* frame = sprite->frame;
    const u16 numSprite = frame->numSprite & 0x7F;
    const FrameVDPSprite* frameSprite = frame->frameVDPSprites;
    const FrameVDPSpriteExt* ext = SPRX_FRAME_EXT(frame);

    const u16 attr = sprite->attribut;
    const u16 baseInd = attr & TILE_INDEX_MASK;
    const u16 basePal = attr & TILE_ATTR_PALETTE_MASK;
    const u16 baseFlip = attr & (TILE_ATTR_VFLIP_MASK | TILE_ATTR_HFLIP_MASK);
    const u16 basePrio = attr & TILE_ATTR_PRIORITY_MASK;
    const bool hflip = (baseFlip & TILE_ATTR_HFLIP_MASK) != 0;
    const bool vflip = (baseFlip & TILE_ATTR_VFLIP_MASK) != 0;
    const s16 sx = sprite->x;
    const s16 sy = sprite->y;

    u8 ind = *vdpSpriteInd;
    s16 visibility = (s16)(sprite->visibility & visibilityMask[(numSprite > 16) ? 16 : numSprite]);

    while(visibility)
    {
        // current sprite visibility bit is in high bit
        if (visibility < 0)
        {
            const u16 e = ext->attr;

            vdpSprite->y = sy + (vflip ? frameSprite->offsetYFlip : frameSprite->offsetY);
            vdpSprite->size = frameSprite->size;
            vdpSprite->link = ind++;
            vdpSprite->attribut = basePrio
                                | ((basePal + (e & TILE_ATTR_PALETTE_MASK)) & TILE_ATTR_PALETTE_MASK)
                                | ((baseFlip ^ e) & (TILE_ATTR_VFLIP_MASK | TILE_ATTR_HFLIP_MASK))
                                | ((baseInd + (e & TILE_INDEX_MASK)) & TILE_INDEX_MASK);
            vdpSprite->x = sx + (hflip ? frameSprite->offsetXFlip : frameSprite->offsetX);
            vdpSprite++;
        }

        frameSprite++;
        ext++;
        visibility <<= 1;
    }

    *vdpSpriteInd = ind;

    return vdpSprite;
}

#endif // MODULE_SPRITE_EXT
