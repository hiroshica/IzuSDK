//======================================================================
// MDSDRV API wrapper for IzuSDK
// (altered from the SGDK wrapper: moved into the library, the driver blob is
//  linked from res/libres.res and MDS_update() runs from the Z80 V-Int hook)
//======================================================================
// Copyright (c) 2020 Ian Karlsson
//
// This software is provided 'as-is', without any express or implied
// warranty. In no event will the authors be held liable for any
// damages arising from the use of this software.
//
// Permission is granted to anyone to use this software for any
// purpose, including commercial applications, and to alter it and
// redistribute it freely, subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must
//    not claim that you wrote the original software. If you use this
//    software in a product, an acknowledgment in the product
//    documentation would be appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must
//    not be misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source
//    distribution.
//======================================================================

/**
 *  \file snd/mdsdrv.h
 *  \brief MDSDRV sound driver (the only music / SFX driver of IzuSDK)
 *
 *  The driver code (mdsdrvdat) is part of the library (res/libres.res).<br>
 *  Each project supplies its own sequence and PCM data made with mdslink
 *  (for instance: BIN mdsseqdat "snd/mdsseq.bin" 2 2 0 NONE FALSE and
 *  BIN mdspcmdat "snd/mdspcm.bin" 32768 2 0 NONE FALSE).<br>
 *  Once MDS_init() succeeds, MDS_update() is called automatically every V-Int
 *  (through the Z80 V-Int callback) until another Z80 driver is loaded.
 */

#ifndef _SND_MDSDRV_H_
#define _SND_MDSDRV_H_

/* Work area size */
#define MDS_WORK_SIZE 512

/* Sound effect / music priority slots */
#define MDS_BGM 3
#define MDS_SE1 2
#define MDS_SE2 1
#define MDS_SE3 0

/* Command numbers */
#define MDS_CMD_GET_CMD_CNT    0
#define MDS_CMD_GET_SOUND_CNT  1
#define MDS_CMD_GET_STATUS     2
#define MDS_CMD_GET_VERSION    3
#define MDS_CMD_GET_GTEMPO     4
#define MDS_CMD_SET_GTEMPO     5
#define MDS_CMD_GET_GVOLUME    6
#define MDS_CMD_SET_GVOLUME    7
#define MDS_CMD_WRITE_FM_PORT0 8
#define MDS_CMD_WRITE_FM_PORT1 9
#define MDS_CMD_FADE_BGM       10
#define MDS_CMD_SET_PAUSE      11
#define MDS_CMD_GET_VOLUME     12
#define MDS_CMD_SET_VOLUME     13
#define MDS_CMD_GET_TEMPO      14
#define MDS_CMD_SET_TEMPO      15

/* Work area, resides in ram */
extern u16 MDS_work[MDS_WORK_SIZE];

/**
 *  \brief
 *      Initialize MDSDRV and make it the current Z80 driver.
 *
 *  \param seqdata Pointer to sequence data file. Must be word aligned.
 *  \param pcmdata Pointer to PCM data file. Must be 32K aligned.
 *  \return 0 on success, non zero when the driver and the sequence data versions don't match.
 *
 *  On success MDS_update() is registered as the Z80 V-Int callback, so it runs every V-Int.
 */
u16 MDS_init(const u8* seqdata, const u8* pcmdata);
/**
 *  \brief
 *      Sound request.
 *
 *  \param slot Request priority (range 0-3, MDS_BGM, MDS_SE1..)
 *  \param id   Sound number. 0 stops the sound playing in this slot.
 */
void MDS_request(u16 slot, u16 id);
/**
 *  \brief
 *      Command request for low-level access (see MDSDRV documentation).
 */
u32 MDS_command(u16 id, u16 param);
/**
 *  \brief
 *      Command request for low-level access (command number >= 0x09).
 */
u32 MDS_command2(u16 id, u16 param1, u16 param2);

/**
 *  \brief
 *      Update the sound driver. Must run once per V-Int.<br>
 *      MDS_init() already registers it as the Z80 V-Int callback, so you don't need to call it.
 */
void MDS_update(void);

/* Wrapper functions */
char* MDS_get_version_str(void);
void MDS_pause(u16 slot, bool state);
void MDS_fade(u8 target, u8 speed, bool stop_when_done);

#endif // _SND_MDSDRV_H_
