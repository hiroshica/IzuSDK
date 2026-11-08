// [EXT] SPRX: host-side test of the SPRJ output + src/mw/sprite/sprx.c, run with qemu-m68k (see run_test.sh).
// Links the rescomp-cs output (test.s) and src/mw/sprite/sprx.c with small stubs for the ISDK functions it calls.

#include "test.h"

int printf(const char* fmt, ...);

// ---- stubs for ISDK functions used by sprx.c ----
static Sprite stubSprite;
Sprite* SPR_addSpriteEx(const SpriteDefinition* def, s16 x, s16 y, u16 attr, u16 flag)
{
    stubSprite.definition = def;
    stubSprite.x = x + 0x80;
    stubSprite.y = y + 0x80;
    stubSprite.attribut = attr;
    stubSprite.status = flag;
    return &stubSprite;
}
void SPR_setPosition(Sprite* s, s16 x, s16 y) { s->x = x + 0x80; s->y = y + 0x80; }
bool SPR_setVRAMTileIndex(Sprite* s, s16 v) { s->attribut = (s->attribut & TILE_ATTR_MASK) | v; return TRUE; }
bool SPR_setDefinition(Sprite* s, const SpriteDefinition* d) { s->definition = d; return TRUE; }
static u16 nextAlloc = 200;
s16 VRAM_alloc(VRAMRegion* r, u16 size) { s16 i = nextAlloc; nextAlloc += size; return i; }
void VRAM_free(VRAMRegion* r, u16 index) { }
static u16 loadedTiles;
bool VDP_loadTileSet(const TileSet* t, u16 index, TransferMethod tm) { loadedTiles = t->numTile; return TRUE; }

// ---- test helpers ----
static int fails = 0;
#define CHECK(cond) do { if (!(cond)) { printf("FAIL line %d: %s\n", __LINE__, #cond); fails++; } } while(0)

static VDPSprite cache[16];

static int fill(Sprite* spr)
{
    u8 ind = 1;
    VDPSprite* end = SPRX_fillVDPSprites(spr, cache, &ind);
    return end - cache;
}

static void dump(int n)
{
    for (int i = 0; i < n; i++)
        printf("  vdp%d: x=%d y=%d size=%02x tile=%d pal=%d h=%d v=%d prio=%d link=%d\n", i,
               cache[i].x - 0x80, cache[i].y - 0x80, cache[i].size,
               cache[i].attribut & TILE_INDEX_MASK, (cache[i].attribut >> 13) & 3,
               (cache[i].attribut >> 11) & 1, (cache[i].attribut >> 12) & 1, (cache[i].attribut >> 15) & 1, cache[i].link);
}

int main(void)
{
    // ---- RESIDENT ----
    const SprxInfo* info = &base_resident;
    CHECK(info->vramMode == SPRX_RESIDENT);
    CHECK(info->vramTiles == 36);
    CHECK(base_resident_VRAM_TILES == 36);
    CHECK(info->residentTiles != NULL && info->residentTiles->numTile == 36);
    CHECK(info->pivotX == 20 && info->pivotY == 16);
    CHECK(info->def->w == 40 && info->def->h == 32);
    CHECK(info->def->maxNumTile == 0 && info->def->maxNumSprite == 3);
    CHECK(info->def->numAnimation == 1);
    CHECK(base_resident_palette.length == 16);

    const Animation* anim = info->def->animations[0];
    CHECK(anim->numFrame == 4 && anim->loop == 0);
    // 0,1,2,1 : frames #1 and #3 share the same data
    CHECK(anim->frames[1] == anim->frames[3]);
    CHECK(anim->frames[0]->timer == 24);            // 100ms / 4.1667
    CHECK(anim->frames[0]->tileset->numTile == 0);  // resident: no transfer

    s16 vram = SPRX_loadResident(info, NULL, DMA);
    CHECK(vram == 200 && loadedTiles == 36);

    Sprite* spr = SPRX_addSprite(info, 100, 80, TILE_ATTR(PAL1, TRUE, FALSE, FALSE), vram, 0);
    CHECK(spr->status & SPR_FLAG_EXT_FRAME);
    CHECK(!(spr->status & SPR_FLAG_AUTO_TILE_UPLOAD));
    CHECK(spr->x - 0x80 == 80 && spr->y - 0x80 == 64);

    spr->frame = anim->frames[1];
    spr->visibility = 0xFFFF;
    int n = fill(spr);
    printf("RESIDENT frame 1:\n");
    dump(n);
    CHECK(n == 3);
    // body (-12,-16) 24x24 : unique part #2 (after part 0: 9 tiles, part 1: 3 tiles) -> tiles 12..20
    CHECK(cache[0].x - 0x80 == 100 - 12 && cache[0].y - 0x80 == 80 - 16);
    CHECK(cache[0].size == SPRITE_SIZE(3, 3));
    CHECK((cache[0].attribut & TILE_INDEX_MASK) == vram + 12);
    CHECK((cache[0].attribut & TILE_ATTR_HFLIP_MASK) == 0);
    CHECK(((cache[0].attribut >> 13) & 3) == PAL1);
    CHECK(cache[0].attribut & TILE_ATTR_PRIORITY_MASK);
    // left side (-20,-16) 8x24 : unique part #3 -> tiles 21..23
    CHECK(cache[1].x - 0x80 == 100 - 20 && (cache[1].attribut & TILE_INDEX_MASK) == vram + 21);
    // right side (12,-16) = left side flipped, same tiles
    CHECK(cache[2].x - 0x80 == 100 + 12 && (cache[2].attribut & TILE_INDEX_MASK) == vram + 21);
    CHECK(cache[2].attribut & TILE_ATTR_HFLIP_MASK);
    CHECK(cache[0].link == 1 && cache[1].link == 2 && cache[2].link == 3);

    // whole sprite H flip: mirrored around pivot, part flips inverted
    spr->attribut |= TILE_ATTR_HFLIP_MASK;
    n = fill(spr);
    printf("RESIDENT frame 1, sprite H flip:\n");
    dump(n);
    CHECK(cache[0].x - 0x80 == 100 - 12);                 // body is centered
    CHECK(cache[0].attribut & TILE_ATTR_HFLIP_MASK);
    CHECK(cache[1].x - 0x80 == 100 + 12);                 // left side goes right
    CHECK(cache[1].attribut & TILE_ATTR_HFLIP_MASK);
    CHECK(cache[2].x - 0x80 == 100 - 20);                 // right side goes left
    CHECK(!(cache[2].attribut & TILE_ATTR_HFLIP_MASK));

    // visibility: only 2nd VDP sprite visible
    spr->attribut &= ~TILE_ATTR_HFLIP_MASK;
    spr->visibility = 0x4000;
    n = fill(spr);
    CHECK(n == 1 && cache[0].x - 0x80 == 80);
    spr->visibility = 0xFFFF;

    // boxes
    SprxRect r;
    CHECK(SPRX_getBoxes(spr) != NULL && SPRX_getBoxes(spr)->numBox == 1);
    CHECK(SPRX_getBox(spr, SPRX_KIND_HIT, 0, &r));
    CHECK(r.x == -16 && r.y == -16 && r.w == 32 && r.h == 24);
    CHECK(!SPRX_getBox(spr, SPRX_KIND_HIT, 1, &r));
    CHECK(!SPRX_getBox(spr, SPRX_KIND_ATTACK, 0, &r));
    spr->attribut |= TILE_ATTR_HFLIP_MASK | TILE_ATTR_VFLIP_MASK;
    CHECK(SPRX_getBox(spr, SPRX_KIND_HIT, 0, &r));
    CHECK(r.x == -16 && r.y == -8 && r.w == 32 && r.h == 24);
    printf("box flipped: %d,%d %dx%d\n", r.x, r.y, r.w, r.h);

    // ---- TRANSFER ----
    info = &base_transfer;
    CHECK(info->vramMode == SPRX_TRANSFER);
    CHECK(info->vramTiles == 12 && info->def->maxNumTile == 12);
    CHECK(info->residentTiles == NULL);
    anim = info->def->animations[0];
    CHECK(anim->frames[0]->timer == 0);                 // timediv = 0
    CHECK(anim->frames[2]->tileset->numTile == 12);     // body 9 + side 3 (flipped side not duplicated)
    spr = SPRX_addSprite(info, 100, 80, TILE_ATTR(PAL2, FALSE, FALSE, FALSE), 300, 0);
    CHECK(spr->status & SPR_FLAG_AUTO_TILE_UPLOAD);
    spr->frame = anim->frames[2];
    spr->visibility = 0xFFFF;
    n = fill(spr);
    printf("TRANSFER frame 2:\n");
    dump(n);
    CHECK((cache[0].attribut & TILE_INDEX_MASK) == 300 && (cache[1].attribut & TILE_INDEX_MASK) == 309);
    CHECK((cache[2].attribut & TILE_INDEX_MASK) == 309 && (cache[2].attribut & TILE_ATTR_HFLIP_MASK));

    // default timediv (16.6667) : 100ms -> 6 frames
    CHECK(base_default.def->animations[0]->frames[0]->timer == 6);

    printf(fails ? "FAILED (%d)\n" : "ALL OK\n", fails);
    return fails ? 1 : 0;
}
