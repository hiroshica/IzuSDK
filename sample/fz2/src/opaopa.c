
#include <opaopa.h>
#include <Stage.h>

//------------------------------------------------
//------------------------------------------------
ff32 EngineSpeed[eEnginesMax+1] = {
    FASTFIX32(0.5),   // sw
    FASTFIX32(1),   // sw
    FASTFIX32(1.5), // bw
    FASTFIX32(2),   // ne
    FASTFIX32(2.5), // je
    FASTFIX32(3),   // te
    FASTFIX32(4),   // re
};
//------------------------------------------------
//------------------------------------------------
void OPAOPA_StartInit(Opaopa_Control *this, Vect2D_ff32 *spos, Sprite *inSpr)
{
    this->m_Pos.x = spos->x;
    this->m_Pos.y = spos->y;
    {
        this->m_spr = inSpr;
        SPR_setAutoTileUpload(this->m_spr, TRUE);
        SPR_setVisibility(this->m_spr, AUTO_FAST);
        SPR_setPriority(this->m_spr, FALSE);
        SPR_setDefinition(this->m_spr, &opaopa);
        u16 ind = VRAM_alloc(&(m_Stage_Control.region_spritesFixed), opaopa.maxNumTile);
        SPR_setVRAMTileIndex(this->m_spr, ind);
        this->m_spr->data |= SPRITE_WORK_USE;
    }
    this->m_AnimIndex = 0;
    this->m_NoFlipFlag = RIGHT_CTRL;
    this->m_FastAnim = false;
    this->m_TouchLine = false;
}

//------------------------------------------------
//------------------------------------------------
void OPAOPA_PreUpdate(Opaopa_Control *this, Stage_Control *mother, bool xf, bool yf)
{
    //
    this->m_FastAnim = false;

    int lr_speedindex = 0;
    bool cf = xf|yf;
    if(cf)
    {
        this->m_FastAnim = true;
        lr_speedindex = this->m_Engines+1;
        if(mother->m_MoveVec.x < FASTFIX32(0))
        {
            this->m_NoFlipFlag = LEFT_CTRL;
            SPR_setHFlip(this->m_spr, true);
        }
        else if(mother->m_MoveVec.x > FASTFIX32(0))
        {
            this->m_NoFlipFlag = RIGHT_CTRL;
            SPR_setHFlip(this->m_spr, false);
        }
    }
    // move player
    this->m_BaseAddPos.x = EngineSpeed[lr_speedindex];
    this->m_BaseAddPos.y = EngineSpeed[lr_speedindex];
    this->m_AddPos.x = FF32_mul(this->m_BaseAddPos.x, mother->m_MoveVec.x);
    this->m_AddPos.y = FF32_mul(this->m_BaseAddPos.y, mother->m_MoveVec.y);
    this->m_OldPos.x = this->m_Pos.x;
    this->m_OldPos.y = this->m_Pos.y;
    this->m_Pos.x += this->m_AddPos.x;
    this->m_Pos.y += this->m_AddPos.y;

    Vect2D_s32 scrpos = Stage_CalcScreen(mother, &(this->m_Pos));
    if(scrpos.x < SPR_LEFT_LIMIT)
    {
        s32 calc = -scrpos.x;
        this->m_Pos.x += FASTFIX32(calc);
        this->m_OldPos.x = this->m_Pos.x;
    }
    else if(scrpos.x > SPR_RIGHT_LIMIT)
    {
        s32 calc = SPR_RIGHT_LIMIT - scrpos.x;
        this->m_Pos.x += FASTFIX32(calc);
        this->m_OldPos.x = this->m_Pos.x;
    }
    s32 checky = FF32_toRoundedInt(this->m_Pos.y);
    if(checky < eUpLine)
    {
        this->m_Pos.y = FASTFIX32(eUpLine);
        this->m_TouchLine = false;
    }
    else if(checky >= eTouchLine)
    {
        this->m_Pos.y = FASTFIX32(eTouchLine);
        this->m_TouchLine = true;
        this->m_ResetXPos = false;
        if(!cf)
        {
            this->m_ResetXPos = true;
        }
        else if(!xf && yf)
        {
            this->m_ResetXPos = true;
        }
        if(this->m_ResetXPos){
            // 操作していなかったら着地なので移動させない(X)
            this->m_Pos.x = this->m_OldPos.x;
        }
    }
    else{
        this->m_TouchLine = false;
    }
}
//------------------------------------------------
//------------------------------------------------
void OPAOPA_Update(Opaopa_Control *this)
{    
}
//------------------------------------------------
//------------------------------------------------
void OPAOPA_PostUpdate(Opaopa_Control *this)
{    
}
//------------------------------------------------
//------------------------------------------------
void OPAOPA_DrawUpdate(Opaopa_Control *this, Stage_Control *mother)
{
    // draw opaopa
    Vect2D_s32 scrpos = Stage_CalcScreen(mother, &(this->m_Pos));
    SPR_setPosition(this->m_spr, (s16)scrpos.x, (s16)scrpos.y);
    //SPR_setVisibility(this->m_spr, HIDDEN);

    s16 newanimindex = this->m_Engines << 1;
    if (this->m_TouchLine)
    {
        // 着地したらアニメーションを停止にする
        newanimindex |= 1;
    }
    if (this->m_AnimIndex != newanimindex)
    {
        // エンジンが変わってたらアニメーションを変更する
        this->m_AnimIndex = newanimindex;
        SPR_setAnim(this->m_spr, newanimindex);
    }
    // draw opaopa

    // animation Speed Control
    if (this->m_FastAnim)
    {
        if (!this->m_TouchLine)
        {
            if (this->m_spr->timer != 0)
            {
                if (this->m_spr->timer <= (int)((12.0f * 0.8f)))
                {
                    SPR_nextFrame(this->m_spr);
                    // kprintf("test OPAOPA call next frame");
                }
            }
        }
        else if(this->m_ResetXPos)
        {
            this->m_spr->timer = this->m_spr->frame->timer;
        }
    }
    else if (this->m_TouchLine)
    {
        this->m_spr->timer = this->m_spr->frame->timer;
    }
}
#if 0
    // kprintf("test OPAOPA timer = %d", (int)this->opaopa_spr->timer);
    // Move Area Check
    if(this->m_Xpos < OPAOPA_LIMIT_L)
    {
        this->m_Area_UpdateX += -(OPAOPA_LIMIT_L - this->m_Xpos);
        this->m_Xpos = OPAOPA_LIMIT_L;
    }
    if(this->m_Xpos > OPAOPA_LIMIT_R)
    {
        this->m_Area_UpdateX += (this->m_Xpos - OPAOPA_LIMIT_R);
        this->m_Xpos = OPAOPA_LIMIT_R;
    }

    // area clop
    if (this->m_Xpos < FASTFIX32(0))
    {
        this->m_Xpos = FASTFIX32(0);
        this->m_FastAnim = false;
    }
    else if (this->m_Xpos > FASTFIX32(320 - 24))
    {
        this->m_Xpos = FASTFIX32(320 - 24);
        this->m_FastAnim = false;
    }

    if (this->m_Ypos < FASTFIX32(0))
    {
        this->m_Ypos = FASTFIX32(0);
    }
    else if (this->m_Ypos >= FASTFIX32(eTouchLine))
    {
        this->m_Ypos = FASTFIX32(eTouchLine);
        this->m_TouchLine = true;
    }
    // area clop
#endif
