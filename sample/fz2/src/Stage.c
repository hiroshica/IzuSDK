//------------------------------------------------------------------
#include <common.h>
#include <Stage.h>


extern StageStartData StageStartDataTable[STAGE_MAX];

/*
typedef struct
{
    u16 w;
    u16 h;
    Palette* palette;
    u16 numAnimation;
    Animation** animations;
    u16 maxNumTile;
    u16 maxNumSprite;
} SpriteDefinition;
*/

//------------------------------------------------
// Sprite test Buffers
//------------------------------------------------
static int testcount = 0;
static int test_index = 0x10;
//------------------------------------------------
//------------------------------------------------
static void SetupVRAM(Stage_Control *this)
{
    /* VRAMのアドレス設定の例 サイズはは64kbyteです。*/
    // 0x0000 - 0x9fffまでCellData
    VDP_setPlaneSize(64, 32, TRUE);
    VDP_setBGAAddress(0xA000);              // BG-A Name Table Area
    VDP_setBGBAddress(0xC000);              // BG-B Name Table Area
    VDP_setWindowAddress(0xE000);           // Window Name Table Area
    VDP_setHScrollTableAddress(0xF000);     // H-Scroll vector Area
    VDP_setSpriteListAddress(0xFC00);       // Sprite Name Table Area

    // VDP_setWindowHPos(FALSE, 0);
    VDP_setWindowVPos(FALSE, 2);
    VDP_setTextPlane(WINDOW);
    VDP_setTextPalette(PAL0);
    // VDP_setHilightShadow(1);
    // VDP_setScrollingMode(HSCROLL_LINE, VSCROLL_PLANE);

    // c-RAM clear
    memsetU16((u16 *)&PaletteWork[0], 0x0000, 4 * 0x10);
    PAL_setColors(0, (const u16 *)&PaletteWork[0], 64, CPU);
}
//------------------------------------------------
//------------------------------------------------
static void SetupASCII(Stage_Control *this)
{
    u16 ind = VRAM_alloc(&(this->region_spritesFixed), 16*3); //128);
    VDP_loadTileSet(&basic_tileset1_0, ind, CPU);
    VDP_loadFont(&ascii_tileset1_0, CPU); // TILE_FONT_INDEX FONT_LEN
    memcpy(&PaletteWork[PAL0], palette_ascii.data, 0x10 * 2);
}
//------------------------------------------------
//------------------------------------------------
void Stage_Init(Stage_Control *this)
{
    this->m_StageNo = 0;
#if 0
    // 旧コード: VRAM_createRegion を SetupVRAM / SPR_init より前に呼んでいたため、
    //   region_BG          = 16 - 783
    //   region_spritesFixed = 784 - 1551
    // になっていた。SetupVRAM で BG-A を 0xA000 に移すとフォントは 1184-1279、BG-Aマップは 1280- になり、
    // さらに SPR_init() が SGDK スプライトVRAM を 764-1183 に確保するため、
    // ASCII / タイトル / OpaOpa のタイルが全部 SGDK スプライト領域の中に入り、自動VRAM確保のスプライトに上書きされていた。
    //kprintf("tile BG:%d SPRITE:%d", BG_TILE_MAX,SPR_TILE_MAX);
    VRAM_createRegion(&(this->region_BG), TILE_USER_INDEX + 0, BG_TILE_MAX);
    VRAM_createRegion(&(this->region_spritesFixed), TILE_USER_INDEX + BG_TILE_MAX, SPR_TILE_MAX);    
    SetupVRAM(this);
    SetupASCII(this);                           // こいつを最初に配置して一番先頭にOpaOpaの武器類を置いておく
    SpriteInit();
#else
    // 修正: 先に VRAM レイアウトを確定させてからリージョンを作る
    SetupVRAM(this);                            // 1. マップアドレス確定 (ここで TILE_MAX_NUM とフォント位置が決まる)
    SpriteInit();                               // 2. SPR_initEx で SGDK スプライトVRAM確定 (TILE_SPRITE_INDEX が決まる)
    // 3. ユーザー領域 [TILE_USER_INDEX, TILE_SPRITE_INDEX) の中にだけ2つのリージョンを作る
    //    (TILE_USER_MAX_INDEX は SGDK の定義上 TILE_SPRITE_INDEX と同じ値でスプライト領域の先頭を指すので、終端は TILE_SPRITE_INDEX を使う)
    VRAM_createRegion(&(this->region_BG), TILE_USER_INDEX, BG_REGION_SIZE);
    VRAM_createRegion(&(this->region_spritesFixed), TILE_USER_INDEX + BG_REGION_SIZE,
                      TILE_SPRITE_INDEX - (TILE_USER_INDEX + BG_REGION_SIZE));
    //kprintf("region BG:%d-%d FIX:%d-%d SPR:%d-", (int)this->region_BG.startIndex, (int)this->region_BG.endIndex,
    //        (int)this->region_spritesFixed.startIndex, (int)this->region_spritesFixed.endIndex, (int)TILE_SPRITE_INDEX);
    SetupASCII(this);                           // こいつを最初に配置して一番先頭にOpaOpaの武器類を置いておく
#endif
    u16 ind = VRAM_alloc(&(this->region_BG), 512);
    BGInit(&(this->m_NowBG), ind);
}
//------------------------------------------------
//------------------------------------------------
void Stage_StartInit(Stage_Control *this)
{    
    //----------------------
    StageStartData *srecord = &StageStartDataTable[this->m_StageNo];
    BGStartInit(&(this->m_NowBG), this->m_StageNo);
    BGBase *brecord = BGGetBaseData( &(this->m_NowBG), srecord->startArea, srecord->startbaseno);
    this->m_AreaNo = srecord->startArea;
    ff32 sx,sy;
    sx = brecord->m_X - FASTFIX32(32);
    sy = brecord->m_Y + FASTFIX32(0);
    this->m_MoveVec.x = FASTFIX32(1);
    this->m_MoveVec.y = FASTFIX32(0);
    this->m_AddViewPort.x = FASTFIX32(0);
    this->m_AddViewPort.y = FASTFIX32(0);
    this->m_ViewPort.x = sx + FASTFIX32(12);    //brecord->m_X;
    this->m_ViewPort.y = FASTFIX32(SCREEN_CETERY);
    this->m_StartPos.x = sx;
    this->m_StartPos.y = sy;
    OPAOPA_StartInit(&(this->m_Opaopa_Control), &(this->m_StartPos), m_Sprite[0]);

    BGSetupArea(&(this->m_NowBG), this->m_AreaNo);
    this->m_OldViewAddMode = this->m_ViewAddMode = VM_None;
    this->m_StartFlag = FALSE;
}
//------------------------------------------------
//------------------------------------------------
void Stage_PreUpdate(Stage_Control *this)
{
    Joy_Record *getJoyRecord = &m_JoyRecord[JOY_1];
    this->m_MoveVec.y = FASTFIX32(0);
    bool joycontrollxf = false;
    bool joycontrollyf = false;

    if (getJoyRecord->m_Data & BUTTON_LEFT)
    {
        this->m_MoveVec.x = FASTFIX32(-1);
        joycontrollxf = true;
    }
    else if (getJoyRecord->m_Data & BUTTON_RIGHT)
    {
        this->m_MoveVec.x = FASTFIX32(1);
        joycontrollxf = true;
    }

    if (getJoyRecord->m_Data & BUTTON_UP)
    {
        this->m_MoveVec.y = FASTFIX32(-1);
        joycontrollyf = true;
    }
    else if (getJoyRecord->m_Data & BUTTON_DOWN)
    {
        this->m_MoveVec.y = FASTFIX32(1);
        joycontrollyf = true;
    }

    // engine test
    if (getJoyRecord->m_Trig & BUTTON_B)
    {
        this->m_Opaopa_Control.m_Engines += 1;
        if (this->m_Opaopa_Control.m_Engines >= eEnginesMax)
        {
            this->m_Opaopa_Control.m_Engines = 0;
        }
    }
    // engine test

    // 座標補正
    ff32 areawidth = FASTFIX32(this->m_NowBG.m_AreaWidth);
    Vect2D_ff32 *getpos = &(this->m_Opaopa_Control.m_Pos);
    if(getpos->x > areawidth){
        getpos->x -= areawidth;
        this->m_ViewPort.x -= areawidth;
    }
    else if(getpos->x < -areawidth){
        getpos->x += areawidth;
        this->m_ViewPort.x += areawidth;
    }
    // 座標補正
    // 現在位置を保存
    this->m_OldViewPort.x = this->m_ViewPort.x;
    this->m_OldViewPort.y = this->m_ViewPort.y;
    // 現在位置を保存

    if(this->m_StartFlag)
    {
        this->m_OldViewAddMode = this->m_ViewAddMode;
        Vect2D_s32 scrpos = Stage_CalcScreen(this, &((this->m_Opaopa_Control).m_Pos));
        //bool cf = joycontrollxf| joycontrollyf;

        if(this->m_Opaopa_Control.m_NoFlipFlag == LEFT_CTRL)
        {
            if(scrpos.x > OPAOPA_LIMIT_R2)
            {
                this->m_ViewAddMode = VM_L_Mode0;
                if(this->m_Opaopa_Control.m_TouchLine)
                {
                    this->m_ViewAddMode = VM_None;
                }
            }
            else if(scrpos.x < OPAOPA_LIMIT_L2)
            {
                this->m_ViewAddMode = VM_L_Mode2;
            }
            else if(joycontrollxf)
            {
                this->m_ViewAddMode = VM_L_Mode0;
                if(this->m_Opaopa_Control.m_TouchLine)
                {
                    this->m_ViewAddMode = VM_None;
                }
            }
            else
            {
                this->m_ViewAddMode = VM_L_Mode1;
            }
        }
        else if(this->m_Opaopa_Control.m_NoFlipFlag == RIGHT_CTRL)
        {
            if(scrpos.x < OPAOPA_LIMIT_L2)
            {
                this->m_ViewAddMode = VM_R_Mode0;
                if(this->m_Opaopa_Control.m_TouchLine)
                {
                    this->m_ViewAddMode = VM_None;
                }
            }
            else if(scrpos.x > OPAOPA_LIMIT_R2)
            {
                this->m_ViewAddMode = VM_R_Mode2;
            }
            else if(joycontrollxf)
            {
                this->m_ViewAddMode = VM_R_Mode0;
                if(this->m_Opaopa_Control.m_TouchLine)
                {
                    this->m_ViewAddMode = VM_None;
                }
            }
            else
            {
                this->m_ViewAddMode = VM_R_Mode1;
            }
        }
        if(this->m_OldViewAddMode != this->m_ViewAddMode)
        {
            switch (this->m_ViewAddMode)
            {
            case VM_L_Mode0:
                this->m_AddViewPort.x = FASTFIX32(-VIEWPORT_MOVEX1);
                break;
            case VM_L_Mode1:
                this->m_AddViewPort.x = FASTFIX32(-VIEWPORT_MOVEX2);
                break;
            case VM_L_Mode2:
                this->m_AddViewPort.x = -this->m_Opaopa_Control.m_BaseAddPos.x;
                break;
            case VM_R_Mode0:
                this->m_AddViewPort.x = FASTFIX32(VIEWPORT_MOVEX1);
                break;
            case VM_R_Mode1:
                this->m_AddViewPort.x = FASTFIX32(VIEWPORT_MOVEX2);
                break;
            case VM_R_Mode2:
                this->m_AddViewPort.x = this->m_Opaopa_Control.m_BaseAddPos.x;
                break;
            default:
                this->m_AddViewPort.x = FASTFIX32(0);
                break;
            }
            //kprintf("new %d", (int)this->m_ViewAddMode);
        }
        else{
            //kprintf("old");
        }
        this->m_ViewPort.x += this->m_AddViewPort.x;
        OPAOPA_PreUpdate(&(this->m_Opaopa_Control), this, joycontrollxf ,joycontrollyf);
    }
#if 0
    {
    Vect2D_s32 scrpos = Stage_CalcScreen(this, &((this->m_Opaopa_Control).m_Pos));
        s32 calc = 0;
        if(scrpos.x < OPAOPA_LIMIT_L2)
        {
            calc = -(OPAOPA_LIMIT_L2 - scrpos.x);
        }
        else if(scrpos.x > OPAOPA_LIMIT_R2)
        {
            calc = (scrpos.x - OPAOPA_LIMIT_R2);
        }
        this->m_ViewPort.x += FASTFIX32(calc);
    }
    {
        fix32 NowCenter = this->m_ViewPort.x;
        fix32 addvx = joycontrollxf ? FASTFIX32(VIEWPORT_MOVEX1) : FASTFIX32(VIEWPORT_MOVEX2);

        if(this->m_Opaopa_Control.m_NoFlipFlag == LEFT_CTRL)
        {
            NowCenter -= FASTFIX32(OPAOPA_LR_OFFSET2);
        }
        else if(this->m_Opaopa_Control.m_NoFlipFlag == RIGHT_CTRL)
        {
            NowCenter += FASTFIX32(OPAOPA_LR_OFFSET2);
        }
        if(this->m_ViewPort.x < NowCenter)
        {
            this->m_ViewPort.x += addvx;
        }
        else if(this->m_ViewPort.x > NowCenter)
        {
            this->m_ViewPort.x -= addvx;
        }
    }
#endif
    BG_PreUpdate(&(this->m_NowBG));
}
//------------------------------------------------
//------------------------------------------------
void Stage_Update(Stage_Control *this)
{
    testcount++;
    if(testcount > 6){
        testcount = 0;
        test_index++;
        if(test_index > 0x100){
            test_index = 0;
        }
        //SPR_setVRAMTileIndex(m_Sprite[0], test_index);
        //SPR_setVisibility(m_Sprite[0], AUTO_FAST);
    }
}
//------------------------------------------------
//------------------------------------------------
void Stage_PostUpdate(Stage_Control *this)
{
}
//------------------------------------------------
//------------------------------------------------
void Stage_DrawUpdate(Stage_Control *this)
{
    //fix32 calcX = this->m_ViewPort.x - this->m_StartPos.x;    // BGはViewPortの現在位置とStart位置のずれ分がスクロール量になる
    ff32 calcX = this->m_ViewPort.x - this->m_OldViewPort.x;
    OPAOPA_DrawUpdate(&(this->m_Opaopa_Control),this);
    BG_DrawUpdate( &(this->m_NowBG), this, calcX);
}

