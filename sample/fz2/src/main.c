//

#include <common.h>
#include <Stage.h>

#if 0
//------------------------------------------------
s16 camPosX = -1;
s16 camPosY = -1;

ff32 posXA = 0;
ff32 posXB = 0;

bool paused = FALSE;

//------------------------------------------------
//------------------------------------------------
void setCameraPosition(s16 x, s16 y, s16 xb, s16 yb)
{
    if (NowMapA != NULL)
    {
        if ((x != camPosX) || (y != camPosY))
        {
            camPosX = x;
            camPosY = y;
            MAP_scrollTo(NowMapA, x, SCRYA_OFFSET+y);
            MAP_scrollTo(NowMapB, xb, SCRYB_OFFSET+yb);
        }
    }
}
//
void BG_Update(ff32 addX)
{
    posXA += addX;
    posXB = fastFix32Mul(posXA, SCRXB_DIV);
    setCameraPosition(fastFix32ToInt(posXA), 0, fastFix32ToInt(posXB),0);
}
#endif
#if 0
//------------------------------------------------
//------------------------------------------------
void handleInput()
{
    Joy_Record *getrecord = &m_JoyRecord[JOY_1];
    if (getrecord->m_Trig & BUTTON_START)
    {
        paused = !paused;
    }

    if (!paused)
    {
        posX += SCRX_ADD;
        posXB = fix32Mul(posX, SCRXB_DIV);
        setCameraPosition(fix32ToInt(posX), fix32ToInt(posY), fix32ToInt(posXB), fix32ToInt(posYB));
        s16 getw = GetStageAreaWidth();
        if (posX >= FIX32((getw * 2) - 320))
        {
            PAL_fadeOutAll(60, TRUE);
            m_JobMode = JOB_FADE_OUT;
        }

        if (getrecord->m_Trig & BUTTON_A)
        {
            PAL_fadeOutAll(60, TRUE);
            m_JobMode = JOB_FADE_OUT;
        }
    }
}
#endif

void Frame1Wait()
{
    SPR_update(); // 1wait
    SYS_doVBlankProcess(); // 1 wait
}

//------------------------------------------------
//------------------------------------------------
void MainInit()
{
#if 0
    {
        const SpriteDefinition *spr = &opaopa;
        kprintf("w:%d h:%d T:%d S:%d A:%d",(int)spr->w,(int)spr->h,(int)spr->maxNumTile,(int)spr->maxNumSprite, (int)spr->numAnimation);
        for(int iI = 0; iI < spr->numAnimation; ++iI)
        {
            Animation *at = spr->animations[iI];
            kprintf("anim(%d) f:%02x l:%02x", iI, at->numFrame, at->loop);
            for(int iJ = 0; iJ < at->numFrame; ++iJ)
            {
                AnimationFrame *af = at->frames[iJ];
                kprintf("         (%d) S:(%02x) T:%02x", iJ, af->numSprite, af->timer);
                for(int iK = 0; iK < af->numSprite; ++iK)
                {
                    FrameVDPSprite *av = &(af->frameVDPSprites[iK]);
                    kprintf("              (%d) T:%02x", iK, av->numTile);
                }
            }
        }
    }
#endif
    JoyInit();
    GlobalWork_Init();
    Stage_Init(&m_Stage_Control);

    m_JobMode = JOB_STAGEINIT;

	if(MDS_init(mdsseqdat, mdspcmdat))
	{
		VDP_drawText("MDSDRV init failed!!!!!", 2, 2);
		VDP_drawText("Please make sure that the driver and", 2, 4);
		VDP_drawText("sequence data version matches.", 2, 5);
	}
	/* MDS_init() makes MDSDRV run every V-Int (no V-Int callback needed) */
}
//------------------------------------------------
//------------------------------------------------
static Sprite *title_B_spr;
static Sprite *title_A1_spr;
static Sprite *title_A2_spr;
void TestTitleInit(Stage_Control *this)
{
    // Title test setup
    memcpy(&PaletteWork[PAL2], title_A1.palette->data, 0x10 * 2); // パレット設定
#if 0
    // 旧コード: VRAM_alloc しているのは title_B の分 (19タイル) だけで、
    // title_A1 / title_A2 は ind を足しただけの「未確保」の場所を使っていた。
    // そのため region_spritesFixed 側ではその場所が空きのままになり、
    // 後の OPAOPA_StartInit の VRAM_alloc が title_A1 と同じ場所を返して上書きし合っていた。
    u16 ind = VRAM_alloc( &(this->region_spritesFixed), title_B.maxNumTile);
    title_B_spr = SPR_addSpriteEx(&title_B, 80, 144, TILE_ATTR_FULL(PAL2, FALSE, FALSE, FALSE, ind), SPR_FLAG_AUTO_TILE_UPLOAD);  // AUTO_VRAM_ALLOC は付けない
    ind += title_B.maxNumTile;
    //Frame1Wait(); // 1 wait
    title_A1_spr = SPR_addSpriteEx(&title_A1, (8 * 8),(8 * 4), TILE_ATTR_FULL(PAL2, FALSE, FALSE, FALSE, ind), SPR_FLAG_AUTO_TILE_UPLOAD);  // AUTO_VRAM_ALLOC は付けない
    ind += title_A1.maxNumTile;
    //title_A1_spr = SPR_addSprite(&title_A1, (8 * 8), (8 * 4), TILE_ATTR(PAL2, FALSE, FALSE, FALSE));
    //Frame1Wait(); // 1 wait
    title_A2_spr = SPR_addSpriteEx(&title_A2, (8 * 20),(8 * 4), TILE_ATTR_FULL(PAL2, FALSE, FALSE, FALSE, ind), SPR_FLAG_AUTO_TILE_UPLOAD);  // AUTO_VRAM_ALLOC は付けない
    ind += title_A2.maxNumTile;
    //title_A2_spr = SPR_addSprite(&title_A2, (8 * 20), (8 * 4), TILE_ATTR(PAL2, FALSE, FALSE, FALSE));
#else
    // 修正: スプライトごとに region_spritesFixed から VRAM_alloc して、使う場所を必ず確保済みにする
    s16 ind;
    ind = VRAM_alloc(&(this->region_spritesFixed), title_B.maxNumTile);
    title_B_spr = SPR_addSpriteEx(&title_B, 80, 144, TILE_ATTR_FULL(PAL2, FALSE, FALSE, FALSE, ind), SPR_FLAG_AUTO_TILE_UPLOAD);  // AUTO_VRAM_ALLOC は付けない
    ind = VRAM_alloc(&(this->region_spritesFixed), title_A1.maxNumTile);
    title_A1_spr = SPR_addSpriteEx(&title_A1, (8 * 8),(8 * 4), TILE_ATTR_FULL(PAL2, FALSE, FALSE, FALSE, ind), SPR_FLAG_AUTO_TILE_UPLOAD);  // AUTO_VRAM_ALLOC は付けない
    ind = VRAM_alloc(&(this->region_spritesFixed), title_A2.maxNumTile);
    title_A2_spr = SPR_addSpriteEx(&title_A2, (8 * 20),(8 * 4), TILE_ATTR_FULL(PAL2, FALSE, FALSE, FALSE, ind), SPR_FLAG_AUTO_TILE_UPLOAD);  // AUTO_VRAM_ALLOC は付けない
#endif
    Frame1Wait(); // 1 wait
}
//------------------------------------------------
//------------------------------------------------
int main()
{
    int16_t JobCount = 0;
    MainInit();
    Frame1Wait();
#if 0
    // 旧コード: タイトルを先に確保していたので、VRAM は ASCII → タイトル → OpaOpa の順になっていた
    TestTitleInit(&m_Stage_Control);
#endif
    bool TitleInitF = FALSE;    // タイトルは最初の Stage_StartInit の後に1回だけ初期化する

    // 表示座標を設定
    // SPR_setPosition(title_A1_spr, (8 * 8), (8 * 4));
    // SPR_setPosition(title_A2_spr, (8 * 20), (8 * 4));
    // SPR_setPosition(title_B_spr, (8 * 10), (8 * 20));

	// MDS_request(MDS_BGM, 1);
    Frame1Wait();
    bool InitF = FALSE;

    while (1)
    {
        // PRE:jobs
        JoyRead();
        Joy_Record *getJoyRecord = &m_JoyRecord[JOY_1];
        int cx = FF32_toRoundedInt(m_Stage_Control.m_Opaopa_Control.m_Pos.x);
        int cy = FF32_toRoundedInt(m_Stage_Control.m_ViewPort.x);
        int ca = FF32_toRoundedInt(m_Stage_Control.m_NowBG.m_PosA.x); // m_Stage_Control.m_Opaopa_Control.m_NoFlipFlag; // m_Stage_Control.m_Opaopa_Control.m_Opaopa_spr->timer;
        sprintf(mesgbbuf, "D%04xT%04x 1=%5d 2=%5d 3=%5d", m_JoyRecord[JOY_1].m_Data, m_JoyRecord[JOY_1].m_Trig, cx,cy,ca);
        VDP_drawTextBG(WINDOW, mesgbbuf, 0, 1);
        // PRE:jobs
        switch (m_JobMode)
        {
        case JOB_STAGEINIT:
            sprintf(mesgbbuf, "STAGE %d-%d", m_Stage_Control.m_StageNo + 1, m_Stage_Control.m_AreaNo + 1);
            VDP_drawTextBG(WINDOW, mesgbbuf, 0, 0);
            Stage_StartInit(&m_Stage_Control);
            // 修正: OpaOpa (Stage_StartInit 内の OPAOPA_StartInit) を先に確保してからタイトルを確保する
            //       VRAM は ASCII → OpaOpa → タイトル の順になる
            if (!TitleInitF)
            {
                TestTitleInit(&m_Stage_Control);
                TitleInitF = TRUE;
            }
            InitF = TRUE;
            m_JobMode = JOB_FADE_IN_START;
            JobCount = 0;
            break;

        case JOB_FADE_IN_START:
            PAL_fadeIn(0, 63, (const u16 *)&PaletteWork[PAL0], 60, FALSE);
            m_JobMode = JOB_FADE_IN;
            break;

        case JOB_FADE_IN:
            if(!PAL_isDoingFade())
            {
                m_JobMode = JOB_IDLE;
                JobCount = 0;
            }
            break;
        case JOB_IDLE:
            if (getJoyRecord->m_Trig & BUTTON_START)
            {
                m_Stage_Control.m_StartFlag = !m_Stage_Control.m_StartFlag;
            }
            JobCount = 0;
            break;

        case JOB_FADE_OUT:
            JobCount++;
            if (JobCount >= 60)
            {
#if 0
                m_JobMode = JOB_IDLE;
                JobCount = 0;
                //
                NowAreaNo++;
                if (NowAreaNo >= GetNowAreaSize())
                {
                    NowStageNo++;
                    if (NowStageNo >= GetStageSize())
                    {
                        NowStageNo = 0;
                    }
                    NowAreaNo = 0;
                }
#endif
                //BG_Exit();
                m_JobMode = JOB_STAGEINIT;
            }
            break;
        default:
            break;
        }
        if(InitF)
        {
            Stage_PreUpdate(&m_Stage_Control);
            Stage_Update(&m_Stage_Control);
            Stage_PostUpdate(&m_Stage_Control);
        }
        Stage_DrawUpdate(&m_Stage_Control);

        //SPR_logProfil();
        //SPR_logSprites();

        SPR_update(); // 実際のスプライトに反映させる
        sprintf(mesgbbuf, "CPU:%d__", SYS_getCPULoad());
        VDP_drawTextBG(WINDOW, mesgbbuf, 10, 0);

        SYS_doVBlankProcess();
    }   // main loop while

    // 以下は誤ってmainloopを抜けてしまった時の対処
    //setCameraPosition(fix32ToInt(posXA), fix32ToInt(posY), fix32ToInt(posXB), fix32ToInt(posYB));
    SYS_doVBlankProcess();

    //
    VDP_drawTextBG(WINDOW, "Game Loop END", 0, 0);
    while (1)
    {
        JoyRead();
        //handleInput();
        SYS_doVBlankProcess();
    }

    return (0);
}
