

#include <BG.h>

// 基地のスプライトは SPRJ (拡張スプライトエンジン SPRX) で表示する。旧 SPR_ON / SPR_MODE1 の手打ち処理は削除

/***************************************************************************/
//extern BG_Record *RoundTable[];
//extern TileSet *StageBaseData[];
//extern s16 RoundStageSize;
//extern s16 RoundAreaSize[];
extern Stage_Record *StageDataTables[STAGE_MAX];

/***************************************************************************/
void BGInit(BG_Control *this, u16 vram)
{
    this->VRAM = vram;
    this->NowMapA = NULL;
    this->NowMapB = NULL;
    for(int iA = 0; iA < BG_AREA_MAX;++iA)
    {
        for(int iB = 0; iB < BG_BASE_MAX; ++iB)
        {
            memset( &this->m_BGBase[iA][iB], 0,sizeof(BGBase) );
        }
    }
    for(int iA = 0; iA < BG_AREA_MAX;++iA)
    {
        for(int iB = 0; iB < BG_BASE_MAX; ++iB)
        {
            memset( &this->m_BGBase[iA][iB], 0,sizeof(BGBase) );
        }
    }
}
/***************************************************************************/
static void BG_Exit(BG_Control *this)
{
    if(this->NowMapA != NULL){
        MEM_free(this->NowMapA);
        this->NowMapA = NULL;
    }
    if(this->NowMapB != NULL){
        MEM_free(this->NowMapB);
        this->NowMapB = NULL;
    }

    for(int iA = 0; iA < BG_AREA_MAX;++iA)
    {
        for(int iB = 0; iB < BG_BASE_MAX; ++iB)
        {
            Sprite *spr = this->m_BGBase[iA][iB].m_Spr;
            if(spr != NULL)
            {
                ReleaseSpriteBuffer(spr);
                SPRX_detach(spr);           // プールに戻すので拡張コマ指定を外す
                this->m_BGBase[iA][iB].m_Spr = NULL;
            }
        }
    }
}
/***************************************************************************/
void BGStartInit(BG_Control *this, s16 stageno)
{
    // All初期化
    BG_Exit(this);
    VDP_setHorizontalScroll(BG_A, 0);
    VDP_setVerticalScroll(BG_A, 0);
    VDP_setHorizontalScroll(BG_B, 0);
    VDP_setVerticalScroll(BG_B, 0);
    for(int iA = 0; iA < BG_AREA_MAX;++iA)
    {
        for(int iB = 0; iB < BG_BASE_MAX; ++iB)
        {
            memset( &this->m_BGBase[iA][iB], 0,sizeof(BGBase) );
        }
    }
    // All初期化

    // BG A 初期化
    Stage_Record *StageData = StageDataTables[stageno];
    this->StageRecord = StageData;
    for(int iA = 0; iA < StageData->m_AreaSize;++iA)
    {
        Area_Record *arecord = &StageData->m_Area[iA];
        for(int iB = 0; iB < arecord->baseSize; ++iB)
        {
            memcpy( &this->m_BGBase[iA][iB], &arecord->m_BGBaseInitData[iB],sizeof(BGBase) );
        }
    }
}
/***************************************************************************/
void BG_PreUpdate(BG_Control *this)
{
}
/***************************************************************************/
// 旧: AnimIndexOffset / ANIM_FRAME による手動アニメは SPRJ の自動アニメ (JSON の durationMs) に置き換えた
//------------------------------------------------
#define BASESPR_W   (8*5)
static s16 CheckScreenLimit(s32 cx)
{
    s16 retcode = LIMIT_NONE;
    if(cx < (SPR_LEFT_LIMIT - BASESPR_W))
    {
        retcode = LEFI_LIMIT_OVER;
    }
    else if(cx > (SPR_RIGHT_LIMIT + BASESPR_W))
    {
        retcode = RIGHT_LIMIT_OVER;
    }
    return retcode;
}
//------------------------------------------------
static Vect2D_s32 StageBase_CalcScreen(Stage_Control *this, Vect2D_ff32 *inpos, s32 pagemask)
{
    Vect2D_s32 retdata;
    s32 cx = FF32_toRoundedInt(inpos->x - this->m_ViewPort.x);
    //s32 cx2;
    if(cx < -SCREEN_WIDTH){
#if 0
        cx = (pagemask - (abs(cx) % pagemask));
#else
        do{
            //kprintf("calc:%d", (int)cx);
            cx += pagemask;
        }
        while(cx < -SCREEN_WIDTH);
#endif
        //kprintf("ans:%d(%d)", (int)cx,(int)cx2);
    }
    else if(cx >= SCREEN_WIDTH){
#if 0
        cx = -pagemask + (cx % pagemask);
#else
        do{
            //kprintf("calc:%d", (int)cx);
            cx -= pagemask;
        }
        while(cx >= SCREEN_WIDTH);
#endif
        //kprintf("ans:%d(%d)", (int)cx,(int)cx2);
    }

    retdata.x = cx + SCREEN_CETERX;
    retdata.y = FF32_toRoundedInt(inpos->y - this->m_ViewPort.y) + SCREEN_CETERY;
    //kprintf("calc X = %d", (int)retdata.x);
    return retdata;
}

void BG_DrawUpdate(BG_Control *this, Stage_Control *mother, ff32 xa_addin)
{
    Area_Record *record = &this->StageRecord->m_Area[this->m_NowArea];
    const SprxInfo *baseinfo = this->StageRecord->m_StageBaseData;

    ff32 xb_addin = FF32_mul(xa_addin, SCRXB_DIV);
    this->m_PosA.x += xa_addin;
    this->m_PosB.x += xb_addin;
    if (this->NowMapA != NULL)
    {
        MAP_scrollTo(this->NowMapA, FF32_toRoundedInt(this->m_PosA.x), FF32_toRoundedInt(this->m_PosA.y));
        MAP_scrollTo(this->NowMapB, FF32_toRoundedInt(this->m_PosB.x), FF32_toRoundedInt(this->m_PosB.y));
    }
    s32 awidth = this->m_AreaWidth;
    //s32 vpage = ff32toRoundedInt(mother->m_ViewPort.x) / awidth;
    //kprintf("v_page:%d", (int)vpage);
    Joy_Record *getJoyRecord = &m_JoyRecord[JOY_1];

    for(int iB = 0; iB < record->baseSize; ++iB)
    //for(int iB = 0; iB < 1; ++iB)
    {
        BGBase *brecord = &this->m_BGBase[this->m_NowArea][iB];
        Sprite *spr = brecord->m_Spr;
        Vect2D_ff32 Pos;
        Pos.x = brecord->m_X;
        Pos.y = brecord->m_Y;

        if((spr != NULL) && (brecord->m_JobMode >= BG_Mode_Active))
        {
            bool drawf = TRUE;
            Vect2D_s32 scrpos = StageBase_CalcScreen(mother, &Pos, awidth);
            s16 retcode = CheckScreenLimit(scrpos.x);
            if(retcode == LEFI_LIMIT_OVER){
                drawf = FALSE;
#if 0
                if(iB==0)
                {
                    kprintf("(%d) = L:%d",iB,(int)scrpos.x);
                }
                Pos.x += ff32(awidth * vpage);
                Vect2D_s32 scrpos = Stage_CalcScreen(mother, &Pos);
                s32 lx = scrpos.x - base_spr.w;
                s32 rx = scrpos.x + base_spr.w;
                if(retcode == LEFI_LIMIT_OVER){
                    drawf = FALSE;
                    //kprintf("left:%d",(int)rx);
                }
                else if(retcode == RIGHT_LIMIT_OVER){
                    drawf = FALSE;
                    //kprintf("right:%d",(int)lx);
                }
#endif
            }
            else if(retcode == RIGHT_LIMIT_OVER){
                drawf = FALSE;
                //kprintf("(%d) = R:%d",iB,(int)rx);
            }
            if(drawf)
            {
                SPR_setHFlip(spr, false);
                SPR_setVFlip(spr, false);
                if (getJoyRecord->m_Data & BUTTON_X)
                {
                    SPR_setHFlip(spr, true);
                }
                if (getJoyRecord->m_Data & BUTTON_Y)
                {
                    SPR_setVFlip(spr, true);
                }


                // scrpos = 基地の基準点 (JSON のキャンバス中心)。アニメとタイルは SPRX が処理する
                SPRX_setPosition(spr, baseinfo, (s16)scrpos.x, (s16)scrpos.y);
                SPR_setVisibility(spr, AUTO_FAST);
            }
            else
            {
                SPR_setVisibility(spr, HIDDEN);
            }
        }
    }
}
/***************************************************************************/
BGBase *BGGetBaseData(BG_Control *this,s16 areano,s16 baseno)
{
    BGBase *ret = &(this->m_BGBase[areano][baseno]);
    return ret;
}
/***************************************************************************/
void BGSetupArea(BG_Control *this, s16 areano)
{
    BG_Exit(this);
    Area_Record *record = &this->StageRecord->m_Area[areano];
    u16 ind = this->VRAM;
    this->m_NowArea = areano;
    this->m_NowFrame = 0;
    this->m_PosA.x = FASTFIX32(0);
    this->m_PosA.y = FASTFIX32(0);
    this->m_PosB.x = FF32_toRoundedInt(FF32_mul(this->m_PosA.x, SCRXB_DIV));
    this->m_PosB.y = FF32_toRoundedInt(FF32_mul(this->m_PosA.y, SCRYB_DIV));

    // setupBase Data
    // 基地のパーツのタイルを region_BG の先頭に置きっぱなしにして (RESIDENT)、全部の基地で共有する
    const SprxInfo *baseinfo = this->StageRecord->m_StageBaseData;
    SPRX_loadResidentAt(baseinfo, ind, CPU);

    for(int iB = 0; iB < record->baseSize; ++iB)
    {
        BGBase *brecord = &this->m_BGBase[this->m_NowArea][iB];
        Sprite *spr = GetSpriteBuffer();
        if(spr != NULL)
        {
            brecord->m_Spr = spr;
            SPRX_setDefinition(spr, baseinfo, ind);     // VRAM はさっき載せた場所を指すだけ (転送しない)
            SPR_setPriority(spr, FALSE);
            SPR_setPalette(spr, PAL1);                  // 色は今までどおりエリアのパレット (PAL1)
            SPR_setVisibility(spr, HIDDEN);             // 表示は BG_DrawUpdate で決める
        }
    }
    ind += baseinfo->vramTiles;

    VDP_loadTileSet(record->tileA, ind, CPU);
    this->NowMapA = MAP_create(record->mapA, BG_A, TILE_ATTR_FULL(PAL1, FALSE, FALSE, FALSE, ind));
    ind += record->tileA->numTile;

    VDP_loadTileSet(record->tileB, ind, CPU);
    this->NowMapB = MAP_create(record->mapB, BG_B, TILE_ATTR_FULL(PAL1, FALSE, FALSE, FALSE, ind));

    MAP_scrollTo(this->NowMapA, FF32_toRoundedInt(this->m_PosA.x), FF32_toRoundedInt(this->m_PosA.y));
    MAP_scrollTo(this->NowMapB, FF32_toRoundedInt(this->m_PosB.x), FF32_toRoundedInt(this->m_PosB.y));

    memcpy(&PaletteWork[PAL1], record->pal->data, 0x10 * 2);
    this->m_AreaWidth = (s32)(this->NowMapA->w) * 128;
    kprintf("now AreaWidth = %d",(int)this->m_AreaWidth);
}
/***************************************************************************/
s16 GetNowAreaSize(BG_Control *this)
{
    s16 retdata = -1;
    if(this->StageRecord != NULL)
    {
        retdata = this->StageRecord->m_AreaSize;
    }
    return retdata;
}
/***************************************************************************/
s16 GetStageAreaWidth(BG_Control *this)
{
    s16 retcode = 512;
    return retcode;
}
