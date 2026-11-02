using System.Text;
namespace Sgdk.Xgm2Tool;




public class XGM
{
    // single XGM supports a maximum of 124-1 = 123 samples

    public List<XGMSample> samples;
    public List<XGMFMCommand> FMcommands;
    public List<XGMPSGCommand> PSGcommands;
    public GD3 gd3;
    public XD3 xd3;
    public bool pal;
    public bool packed;

    public XGM()
    {
        samples = new();
        FMcommands = new();
        PSGcommands = new();
        gd3 = null;
        xd3 = null;
        pal = false;
        packed = false;
    }

    public XGM(byte[] data) : this()
    {

        int flags;
        int offset, len;
        int pcmLen, fmLen, psgLen;

        if (!Launcher.silent)
            Console.WriteLine("Parsing XGM file...");

        if (!"XGM2".EqualsIgnoreCaseJ(Util.getASCIIString(data, 0, 4)))
            throw new ArgumentException("Error: XGM2 file not recognized !");

        // 0004: version (0x10 currently)
        Util.getInt8(data, 4);
        // 0005: format description (see xgm2.txt)
        flags = Util.getInt8(data, 5);

        // bit #0: NTSC / PAL information: 0=NTSC 1=PAL
        pal = (flags & 1) != 0;
        // bit #1: multi tracks file: 0=No 1=Yes (always 0 here)
        if ((flags & 2) != 0)
            throw new NotSupportedException("Cannot convert from multi tracks XGM file !");

        // bit #2: GD3 tags: 0=No 1=Yes
        if ((flags & 4) != 0)
            gd3 = new GD3();
        // bit #3: packed FM / PSG / GD3 data blocks: 0=No 1=Yes
        packed = (flags & 8) != 0;

        // 0006-0007: SLEN = Sample data bloc size / 256 (ex: $0200 means 512*256 = 131072 bytes)
        pcmLen = Util.getInt16(data, 0x0006) * 256;
        // 0008-0009: FMLEN = FM music data block size / 256 (ex: $0040 means 64*256 = 16384 bytes)
        fmLen = Util.getInt16(data, 0x0008) * 256;
        // 000A-000B: PSGLEN = PSG music data block size / 256 (ex: $0020 means 32*256 = 8192 bytes)
        psgLen = Util.getInt16(data, 0x000A) * 256;

        // 000C-0103: sample id table (size = 124 entries = 248 bytes)
        for (int s = 0; s < (124 - 1); s++)
        {
            // get sample address
            int addr = Util.getInt16(data, ((s + 0) * 2) + 0x000C);
            // get next sample address (end address)
            int naddr = Util.getInt16(data, ((s + 1) * 2) + 0x000C);

            // does we have a sample ?
            if ((addr != 0xFFFF) && (naddr != 0xFFFF))
                // add sample (id 0 is reserved for stop operation)
                samples.Add(new XGMSample(s + 1, data, 0x0104 + (addr << 8), (naddr - addr) << 8));
        }

        // calculate music data offset (0x0104 + sample data block size)
        offset = 0x0104 + pcmLen;

        if (packed)
        {
            // build FM command list
            parseFMMusic(XGCPacker.unpack(J.CopyOfRange(data, offset, offset + fmLen), null));
            // build PSG command list
            parsePSGMusic(XGCPacker.unpack(J.CopyOfRange(data, offset + fmLen, offset + fmLen + psgLen), null));

            // FIXME: loop point is not properly restored for packed XGM (loop address has been adjusted for compressed
            // data)
            // for now we just lost the loop point
            setFMLoopAddress(0);
            setPSGLoopAddress(0);
        }
        else
        {
            // build FM command list
            parseFMMusic(J.CopyOfRange(data, offset, offset + fmLen));
            // build PSG command list
            parsePSGMusic(J.CopyOfRange(data, offset + fmLen, offset + fmLen + psgLen));
        }

        updateTimes();
        updateOffsets();

        // GD3 tags ?
        if (gd3 != null)
        {
            // packed enabled ?
            if (packed)
            {
                xd3 = new XD3(data, offset + fmLen + psgLen);
                gd3 = new GD3(xd3);
            }
            else
            {
                gd3 = new GD3(data, offset + fmLen + psgLen);
                xd3 = new XD3(gd3, getTotalTimeInFrame(), getLoopDurationInFrame());
            }
        }

        if (!Launcher.silent)
        {
            Console.WriteLine("Number of PCM sample: " + samples.Count);
            Console.WriteLine("Number of FM command : " + FMcommands.Count);
            Console.WriteLine("Number of PSG command : " + PSGcommands.Count);
            Console.WriteLine("PCM data size: " + getPCMDataSize());

            if (packed)
            {
                try
                {
                    Console.WriteLine("FM music data size: " + getFMMusicDataSize() + " - packed size: " + getPackedFMMusicDataArray().Length);
                    Console.WriteLine("PSG music data size: " + getPSGMusicDataSize() + " - packed size: " + getPackedPSGMusicDataArray().Length);
                }
                catch (IOException e)
                {
                    Console.WriteLine("FM music data size: " + getFMMusicDataSize());
                    Console.WriteLine("PSG music data size: " + getPSGMusicDataSize());
                }
            }
            else
            {
                Console.WriteLine("FM music data size: " + getFMMusicDataSize());
                Console.WriteLine("PSG music data size: " + getPSGMusicDataSize());
            }

            Console.WriteLine("XGM duration: " + getTotalTimeInFrame() + " frames (" + getTotalTimeInSecond() + " seconds) - loop: " + getLoopDurationInFrame()
                    + " frames (" + getLoopDurationInSecond() + " seconds)");
        }
    }

    public XGM(VGM vgm, bool pack) : this()
    {

        if (!Launcher.silent)
            Console.WriteLine("Converting VGM to XGM...");

        if (vgm.rate == 50)
            pal = true;
        else
            pal = false;

        gd3 = vgm.gd3;
        packed = pack;

        try
        {
            // first we extract samples from VGM
            extractSamples(vgm);
        }
        catch (IOException e)
        {
            e.ToString();
        }
        // then we extract music data
        extractMusic(vgm);
        // XGM optimization
        optimizeCommands();
        // samples optimization
        optimizeSamples();

        // update times and offsets
        updateTimes();
        updateOffsets();

        // then update loop points offset
        updateLoopOffsets();

        // build XD3 after duration has been computed
        if (gd3 != null)
            xd3 = new XD3(gd3, getTotalTimeInFrame(), getLoopDurationInFrame());

        if (!Launcher.silent)
        {
            Console.WriteLine("Number of PCM sample: " + samples.Count);
            Console.WriteLine("Number of FM command : " + FMcommands.Count);
            Console.WriteLine("Number of PSG command : " + PSGcommands.Count);
            Console.WriteLine("PCM data size: " + getPCMDataSize());

            if (packed)
            {
                try
                {
                    Console.WriteLine("FM music data size: " + getFMMusicDataSize() + " - packed size: " + getPackedFMMusicDataArray().Length);
                    Console.WriteLine("PSG music data size: " + getPSGMusicDataSize() + " - packed size: " + getPackedPSGMusicDataArray().Length);
                }
                catch (IOException e)
                {
                    Console.WriteLine("FM music data size: " + getFMMusicDataSize());
                    Console.WriteLine("PSG music data size: " + getPSGMusicDataSize());
                }
            }
            else
            {
                Console.WriteLine("FM music data size: " + getFMMusicDataSize());
                Console.WriteLine("PSG music data size: " + getPSGMusicDataSize());
            }

            Console.WriteLine("XGM duration: " + getTotalTimeInFrame() + " frames (" + getTotalTimeInSecond() + " seconds) - loop: " + getLoopDurationInFrame()
                    + " frames (" + getLoopDurationInSecond() + " seconds)");
        }
    }

    public void parseFMMusic(byte[] data)
    {
        // parse all XGM commands
        int off = 0;
        while (off < data.Length)
        {
            XGMFMCommand command = new XGMFMCommand(data, off);

            FMcommands.Add(command);
            off += command.size;

            // stop here (need to check for it as data block is aligned on 256 bytes)
            if (command.isLoop())
                break;
        }
    }

    public void parsePSGMusic(byte[] data)
    {
        // parse all XGM commands
        int off = 0;
        while (off < data.Length)
        {
            XGMPSGCommand command = new XGMPSGCommand(data, off);

            PSGcommands.Add(command);
            off += command.size;

            // stop here (need to check for it as data block is aligned on 256 bytes)
            if (command.isLoop())
                break;
        }
    }

    public void extractSamples(VGM vgm)
    {
        // extract samples
        foreach (SampleBank bank in vgm.sampleBanks)
        {
            foreach (SampleBank.InternalSample sample in bank.samples)
                // start from id 1 (0 is reserved for silent sample)
                samples.Add(XGMSample.createFromVGMSample(samples.Count + 1, sample));
        }
    }

    public void extractMusic(VGM vgm)
    {
        // need to classify by channel
        JMap<int, VGMCommand> ymKeyCommands = new();
        JMap<int, List<VGMCommand>> ymChannelSetCommands = new();
        JMap<int, int> ymFreqCommands = new();
        List<VGMCommand> ymMiscCommands = new();
        List<VGMCommand> psgCommands = new();
        List<VGMCommand> sampleCommands = new();
        List<VGMCommand> frameCommands = new();

        List<XGMFMCommand> newFMCommands = new();
        List<XGMPSGCommand> newPSGCommands = new();

        int index = 0;
        int highFreqLatch = 0;

        while (index < vgm.commands.Count)
        {
            // prepare new commands for this frame
            newFMCommands.Clear();
            newPSGCommands.Clear();

            int frameToWait = 0;

            // get frame commands
            frameCommands.Clear();
            while (index < vgm.commands.Count)
            {
                VGMCommand command = vgm.commands[index++];

                if (command.isLoopStart())
                {
                    newFMCommands.Add(new XGMFMCommand.LoopStartCommand());
                    newPSGCommands.Add(new XGMPSGCommand.LoopStartCommand());
                    continue;
                }
                // ignore data block
                if (command.isDataBlock())
                    continue;
                // wait command ?
                if (command.isWait())
                {
                    // get wait
                    frameToWait += command.getWaitValue();

                    // check if next commands are wait too
                    while (index < vgm.commands.Count)
                    {
                        VGMCommand nextCom = vgm.commands[index];

                        // not a wait --> stop here
                        if (!nextCom.isWait())
                            break;

                        frameToWait += nextCom.getWaitValue();
                        index++;
                    }

                    // stop here
                    break;
                }
                // stop here
                if (command.isEnd())
                    break;

                // add command
                frameCommands.Add(command);
            }

            // group commands
            ymKeyCommands.Clear();
            ymChannelSetCommands.Clear();
            ymFreqCommands.Clear();
            ymMiscCommands.Clear();
            psgCommands.Clear();
            sampleCommands.Clear();

            foreach (VGMCommand command in frameCommands)
            {
                int ch;
                int chKey;

                if (command.isStream())
                    sampleCommands.Add(command);
                else if (command.isPSGWrite())
                    psgCommands.Add(command);
                // YM command
                else if (command.isYM2612Write())
                {
                    ch = command.getYM2612Channel();
                    chKey = (int)(ch);

                    // we have a key event pending for this channel ? --> transfer all previous commands now
                    if (ymKeyCommands[chKey] != null)
                        newFMCommands.AddRange(compileYMCommands(ymChannelSetCommands, ymFreqCommands, ymMiscCommands, ymKeyCommands));

                    // frequency set command
                    if (command.isYM2612FreqWrite())
                    {
                        int reg = command.getYM2612Register();

                        // FIXME: it seems that register 0xA4 and 0xAC has 2 separates latch
                        // case 0xa0:
                        // chip->fnum[channel] = (chip->data & 0xff) | ((chip->reg_a4 & 0x07) << 8);
                        // chip->block[channel] = (chip->reg_a4 >> 3) & 0x07;
                        // chip->kcode[channel] = (chip->block[channel] << 2) | fn_note[chip->fnum[channel] >> 7];
                        // break;
                        // case 0xa4:
                        // chip->reg_a4 = chip->data & 0xff;
                        // break;
                        // case 0xa8:
                        // chip->fnum_3ch[channel] = (chip->data & 0xff) | ((chip->reg_ac & 0x07) << 8);
                        // chip->block_3ch[channel] = (chip->reg_ac >> 3) & 0x07;
                        // chip->kcode_3ch[channel] = (chip->block_3ch[channel] << 2) | fn_note[chip->fnum_3ch[channel]
                        // >> 7];
                        // break;
                        // case 0xac:
                        // chip->reg_ac = chip->data & 0xff;
                        // break;

                        // high byte !! having a single latch should be ok as we sort commands by channel / operator !
                        if ((reg & 4) == 4)
                            highFreqLatch = (command.getYM2612Value() & 0x3F) << 8;
                        // low part, we can write it with last high part latch
                        else
                        {
                            int c;

                            // special mode freq set ? --> keep slot info (ch8-10)
                            if ((reg & 8) != 0)
                                c = 8 + (reg & 3);
                            else
                                c = ch;

                            ymFreqCommands.put((int)(c), (int)(command.getYM2612Value() | highFreqLatch));
                        }
                    }
                    else if (command.isYM2612KeyWrite())
                        ymKeyCommands.put(chKey, command);
                    // general channel set command
                    else if (command.isYM2612ChannelSet())
                    {
                        List<VGMCommand> coms = ymChannelSetCommands[chKey];

                        if (coms == null)
                        {
                            // build the list from this command
                            coms = new();
                            coms.Add(command);
                            ymChannelSetCommands.put(chKey, coms);
                        }
                        // simply add the command in the list
                        else
                            coms.Add(command);
                    }
                    // YM misc command
                    else
                        ymMiscCommands.Add(command);
                }
                else if (Launcher.verbose)
                    Console.WriteLine("Command " + command + " ignored");
            }

            // YM commands first
            newFMCommands.AddRange(compileYMCommands(ymChannelSetCommands, ymFreqCommands, ymMiscCommands, ymKeyCommands));
            // then PCM commands
            if (!sampleCommands.IsEmptyJ())
                newFMCommands.AddRange(XGMFMCommand.createPCMCommands(this, sampleCommands));
            // and finally PSG commands (need to have them separate as they can't be processed during DMA)
            if (!psgCommands.IsEmptyJ())
                newPSGCommands.AddRange(XGMPSGCommand.createPSGCommands(psgCommands));

            // last frame ?
            if (index >= vgm.commands.Count)
            {
                // add end command (loop command internally, offset will be computed later)
                newFMCommands.Add(new XGMFMCommand.EndCommand());
                newPSGCommands.Add(new XGMPSGCommand.EndCommand());
            }
            else
            {
                while (frameToWait > 0)
                {
                    // end frame
                    newFMCommands.Add(new XGMFMCommand.FrameCommand());
                    // add end frame
                    newPSGCommands.Add(new XGMPSGCommand.FrameCommand());

                    if (pal)
                        frameToWait -= 882;
                    else
                        frameToWait -= 735;
                }
            }

            // finally add the new commands
            FMcommands.AddRange(newFMCommands);
            PSGcommands.AddRange(newPSGCommands);
        }
    }

    public void setFMLoopAddress(int addr)
    {
        // Loop is the last command
        XGMFMCommand loopCom = FMcommands[FMcommands.Count - 1];
        // set loop address
        loopCom.setLoopAddr(addr);
    }

    public void setPSGLoopAddress(int addr)
    {
        // Loop is the last command
        XGMPSGCommand loopCom = PSGcommands[PSGcommands.Count - 1];
        // set loop address
        loopCom.setLoopAddr(addr);
    }

    public void updateLoopOffsets()
    {
        XGMFMCommand fmLoopStartCom = getFMLoopStartCommand();
        XGMPSGCommand psgLoopStartCom = getPSGLoopStartCommand();

        if (fmLoopStartCom != null)
            setFMLoopAddress(fmLoopStartCom.getOriginOffset());
        if (psgLoopStartCom != null)
            setPSGLoopAddress(psgLoopStartCom.getOriginOffset());
    }

    public JMap<int, List<XGMFMCommand>> getFMCommandsPerFrame()
    {
        JMap<int, List<XGMFMCommand>> result = new();

        int c = 0;
        int frame = 0;
        while (c < FMcommands.Count)
        {
            List<XGMFMCommand> frameCommands = new();

            // not a wait command ?
            while (!FMcommands[c].isWait(false))
            {
                // add command to frame list
                frameCommands.Add(FMcommands[c]);

                // done ? --> stop here
                if (++c >= FMcommands.Count)
                    break;
            }

            int key = (int)(frame);

            if (c < FMcommands.Count)
            {
                // get wait command
                XGMFMCommand com = FMcommands[c];
                // add it to frame list
                frameCommands.Add(com);

                // next frame
                frame += FMcommands[c].getWaitFrame();
                c++;
            }

            // add commands for this frame
            result.put(key, frameCommands);
        }

        return result;
    }

    public JMap<int, List<XGMPSGCommand>> getPSGCommandsPerFrame()
    {
        JMap<int, List<XGMPSGCommand>> result = new();

        int c = 0;
        int frame = 0;
        while (c < PSGcommands.Count)
        {
            List<XGMPSGCommand> frameCommands = new();

            // not a wait command ?
            while (!PSGcommands[c].isWait(false))
            {
                // add command to frame list
                frameCommands.Add(PSGcommands[c]);

                // done ? --> stop here
                if (++c >= PSGcommands.Count)
                    break;
            }

            int key = (int)(frame);

            if (c < PSGcommands.Count)
            {
                // get wait command
                XGMPSGCommand com = PSGcommands[c];
                // add it to frame list
                frameCommands.Add(com);

                // next frame
                frame += PSGcommands[c].getWaitFrame();
                c++;
            }

            // add commands for this frame
            result.put(key, frameCommands);
        }

        return result;
    }

    public void packWaitFM()
    {
        int c = 0;
        while (c < FMcommands.Count)
        {
            // get next wait command
            while (!FMcommands[c].isWait(true))
            {
                // done ? --> stop here
                if (++c >= FMcommands.Count)
                    return;
            }

            int startInd = c;
            int wait = 0;
            // get next no wait command
            while (FMcommands[c].isWait(true))
            {
                // sum wait
                wait += FMcommands[c].getWaitFrame();
                // done ? --> stop here
                if (++c >= FMcommands.Count)
                    break;
            }

            // remove all wait commands
            while (c > startInd)
                FMcommands.remove(--c);

            // add new wait commands
            List<XGMFMCommand> waitComs = XGMFMCommand.createWaitCommands(wait);
            FMcommands.AddRange(startInd, waitComs);

            // next
            c += waitComs.Count;
        }
    }

    public void packWaitPSG()
    {
        int c = 0;
        while (c < PSGcommands.Count)
        {
            // get next wait command
            while (!PSGcommands[c].isWait(true))
            {
                // done ? --> stop here
                if (++c >= PSGcommands.Count)
                    return;
            }

            int startInd = c;
            int wait = 0;
            // get next no wait command
            while (PSGcommands[c].isWait(true))
            {
                // sum wait
                if (!PSGcommands[c].isDummy())
                    wait += PSGcommands[c].getWaitFrame();

                // done ? --> stop here
                if (++c >= PSGcommands.Count)
                    break;
            }

            // remove all wait commands
            while (c > startInd)
                PSGcommands.remove(--c);

            // add new wait commands
            List<XGMPSGCommand> waitComs = XGMPSGCommand.createWaitCommands(wait);
            PSGcommands.AddRange(startInd, waitComs);

            // next
            c += waitComs.Count;
        }
    }

    public static List<XGMFMCommand> optimizeSetPanning(int port, int ch, List<XGMFMCommand> fmWriteCHCommands, YM2612State ymState)
    {
        List<XGMFMCommand> result = new();

        foreach (XGMFMCommand com in fmWriteCHCommands)
            result.AddRange(XGMFMCommand.convertToSetPanningCommands(port, ch, com, ymState));

        return result;
    }

    public static List<XGMFMCommand> optimizeSetTL(int port, int ch, List<XGMFMCommand> fmWriteCHCommands)
    {
        List<XGMFMCommand> result = new();

        foreach (XGMFMCommand com in fmWriteCHCommands)
            result.AddRange(XGMFMCommand.convertToSetTLCommands(port, ch, com));

        return result;
    }

    public static List<XGMFMCommand> optimizeLoadInst(int port, int ch, List<XGMFMCommand> fmWriteCHCommands, YM2612State ymState)
    {
        List<XGMFMCommand> result = new();

        // load inst command is 31 bytes
        // we optimize as soon FM writes are >= 16 bytes (longer to parse and load inst command compress better)
        if (Command.getDataSize(fmWriteCHCommands) >= 16)
        {
            XGMFMCommand com = XGMFMCommand.convertToLoadInstCommand(port, ch, fmWriteCHCommands, ymState);

            if (com != null)
                result.Add(com);
        }

        return result;
    }

    public void removeDuplicateSetFreq(List<XGMFMCommand> fmChannelCommands)
    {
        bool freqSet = false;

        // only keep the last one so we start by the end
        for (int c = fmChannelCommands.Count - 1; c >= 0; c--)
        {
            XGMFMCommand com = fmChannelCommands[c];

            // don't optimize special mode freq set
            if (com.isYMFreqSpecialWrite() || com.isYMFreqDeltaSpecialWrite())
                continue;

            if (com.isYMFreqWrite() || com.isYMFreqDeltaWrite())
            {
                // already set ? --> set dummy
                if (freqSet)
                    com.setDummy();
                else
                    freqSet = true;
            }
        }
    }

    public void cleanKeyCommands(List<XGMFMCommand> fmChannelCommands)
    {
        bool hasKeyOn = false;
        bool hasKeyOff = false;
        // bool hasLoadInst = false;

        // start from end of frame
        for (int c = fmChannelCommands.Count - 1; c >= 0; c--)
        {
            XGMFMCommand com = fmChannelCommands[c];

            if (com.isYMKeyONWrite())
            {
                // can't have several key-on in a single frame
                if (hasKeyOn)
                    com.setDummy();
                else
                    hasKeyOn = true;
            }
            else if (com.isYMKeyOFFWrite())
            {
                // can't have more than 2 key-off in a single frame
                if (hasKeyOff)
                    com.setDummy();
                else
                {
                    // allow an extra key-off after a key-on
                    if (hasKeyOn)
                        hasKeyOff = true;

                    // // ignore any key-off before a load inst command
                    // if (hasLoadInst)
                    // com.setDummy();
                }
            }
            // else if (com.isYMLoadInst())
            // hasLoadInst = true;
        }

        bool? keyState = null;
        // then in normal frame order
        foreach (XGMFMCommand com in fmChannelCommands)
        {
            // if (com.isYMLoadInst())
            // keyState = false;
            // else
            if (com.isYMKeyONWrite())
            {
                // already on
                if (keyState == true)
                    com.setDummy();

                keyState = true;
            }
            else if (com.isYMKeyOFFWrite())
            {
                // already off
                if (keyState == false)
                    com.setDummy();

                keyState = false;
            }
        }
    }

    public void optimizeKeySeqCommands(List<XGMFMCommand> fmChannelCommands)
    {
        XGMFMCommand lastKeyOFF = null;
        XGMFMCommand lastKeyON = null;

        foreach (XGMFMCommand com in fmChannelCommands)
        {
            if (com.isYMKeyOFFWrite())
            {
                // combine key on-off seq
                if (lastKeyON != null)
                {
                    lastKeyON.setDummy();
                    lastKeyON = null;
                    com.toKeySeq(true);
                }
                else
                    lastKeyOFF = com;
            }
            else if (com.isYMKeyONWrite())
            {
                // combine key off-on seq
                if (lastKeyOFF != null)
                {
                    lastKeyOFF.setDummy();
                    lastKeyOFF = null;
                    com.toKeySeq(false);
                }
                else
                    lastKeyON = com;
            }
            else if (com.isYMSetTL() || com.isYMKeyAdvWrite() || com.isYMLoadInst() || com.isYMWrite())
            {
                // can't combine
                lastKeyOFF = null;
                lastKeyON = null;
            }
        }
    }

    public void combineSetFreqKeyCommands(List<XGMFMCommand> fmChannelCommands)
    {
        bool canCombine = false;
        XGMFMCommand lastKeyOFF = null;
        XGMFMCommand lastKeyON = null;

        // safe key-on combine (start from end of frame)
        for (int c = fmChannelCommands.Count - 1; c >= 0; c--)
        {
            XGMFMCommand com = fmChannelCommands[c];

            // the driver does not support key on/off on setFreq special
            if (com.isYMFreqWrite() && !com.isYMFreqSpecialWrite())
            {
                if (canCombine)
                {
                    com.setYMFreqKeyON();
                    lastKeyON.setDummy();
                }
                canCombine = false;
            }
            else if (com.isYMKeyONWrite())
            {
                canCombine = true;
                lastKeyON = com;
            }
            else if (com.isYMKeyOFFWrite())
                canCombine = false;
            else if (com.isYMSetTL() || com.isYMKeyAdvWrite() || com.isYMKeySequence() || com.isYMLoadInst() || com.isYMWrite())
                canCombine = false;
        }

        canCombine = false;
        // safe key-off combine (start from beginning of frame)
        for (int c = 0; c < fmChannelCommands.Count; c++)
        {
            XGMFMCommand com = fmChannelCommands[c];

            // the driver does not support key on/off on setFreq special
            if (com.isYMFreqWrite() && !com.isYMFreqSpecialWrite())
            {
                if (canCombine)
                {
                    com.setYMFreqKeyOFF();
                    lastKeyOFF.setDummy();
                }
                canCombine = false;
            }
            else if (com.isYMKeyOFFWrite())
            {
                canCombine = true;
                lastKeyOFF = com;
            }
            else if (com.isYMKeyONWrite())
                canCombine = false;
            else if (com.isYMSetTL() || com.isYMKeyAdvWrite() || com.isYMKeySequence() || com.isYMLoadInst() || com.isYMWrite())
                canCombine = false;
        }

        bool hasKeyOn = false;
        bool canCombineOFF = false;
        bool canCombineON = false;
        XGMFMCommand setFreq = null;

        // isolated key-off/on sequence combine
        foreach (XGMFMCommand com in fmChannelCommands)
        {
            if (com.isYMKeyOFFWrite())
            {
                // combine key off only if we don't have a keyOn before
                if (!hasKeyOn)
                {
                    canCombineOFF = true;
                    lastKeyOFF = com;
                }
            }
            else if (com.isYMKeyONWrite())
            {
                canCombineON = true;
                hasKeyOn = true;
                lastKeyON = com;
            }
            else if (com.isYMSetTL() || com.isYMKeyAdvWrite() || com.isYMKeySequence() || com.isYMLoadInst() || com.isYMWrite())
            {
                // meet a set freq command ? --> combine now
                if (setFreq != null)
                {
                    if (canCombineOFF)
                    {
                        setFreq.setYMFreqKeyOFF();
                        lastKeyOFF.setDummy();
                    }
                    if (canCombineON)
                    {
                        setFreq.setYMFreqKeyON();
                        lastKeyON.setDummy();
                    }
                }
                
                // done
                canCombineOFF = false;
                canCombineON = false;
                setFreq = null;
                hasKeyOn = false;
            }
            // the driver does not support key on/off on setFreq special
            else if (com.isYMFreqWrite() && !com.isYMFreqSpecialWrite())
            {
                setFreq = com;
                if (com.isYMFreqWithKeyON())
                    hasKeyOn = true;
            }
        }

        // last set Freq to combine ?
        if (setFreq != null)
        {
            if (canCombineOFF)
            {
                setFreq.setYMFreqKeyOFF();
                lastKeyOFF.setDummy();
            }
            if (canCombineON)
            {
                setFreq.setYMFreqKeyON();
                lastKeyON.setDummy();
            }
        }
    }

    public void packFMFreqCommands(List<XGMFMCommand> fmChannelCommands)
    {
        int[] lastFreq = new int[] {-1, -1, -1, -1};

        foreach (XGMFMCommand com in fmChannelCommands)
        {
            // reset state
            if (com.isLoopStart())
                J.Fill(lastFreq, -1);
            else if (com.isYMFreqWrite())
            {
                int freq = com.getYMFreqValue();
                int slot = com.isYMFreqSpecialWrite() ? com.getYMSlot() : 0;

                // cannot pack set Freq with key event
                if ((lastFreq[slot] != -1) && !com.isYMFreqWithKeyWrite())
                {
                    // compute delta
                    int delta = freq - lastFreq[slot];

                    if (delta == 0)
                    {
                        // this can happen when we removed duplicate set freq --> set dummy
                        // Console.WriteLine("FM freq delta = 0");
                        com.setDummy();
                    }
                    else if (Math.Abs(delta) <= 128)
                        com.toFreqDelta(delta);
                }

                lastFreq[slot] = freq;
            }
        }
    }

    public void convertFMTLCommands(List<XGMFMCommand> fmChannelCommands)
    {
        int[] lastTL = new int[] {-1, -1, -1, -1};

        foreach (XGMFMCommand com in fmChannelCommands)
        {
            // reset state
            if (com.isLoopStart())
                J.Fill(lastTL, -1);
            else if (com.isYMLoadInst())
            {
                // set TL state from load_inst command
                for (int s = 0; s < 4; s++)
                    lastTL[s] = com.data[1 + 4 + s] & 0x7F;
            }
            else if (com.isYMSetTL())
            {
                int tl = com.getYMTLValue();
                int slot = com.getYMSlot();

                if (lastTL[slot] != -1)
                {
                    // compute delta
                    int delta = tl - lastTL[slot];

                    if (delta == 0)
                    {
                        if (Launcher.verbose)
                            Console.WriteLine("TL delta = 0");
                        com.setDummy();
                    }
                    else if (Math.Abs(delta) <= 64)
                        com.toTLDelta(delta);
                }

                lastTL[slot] = tl;
            }
        }
    }

    public int useExtWaitFMCommand(List<XGMFMCommand> fmFrameCommands)
    {
        // no need to process
        if (fmFrameCommands.Count <= 1)
            return -1;

        XGMFMCommand waitCom = fmFrameCommands[fmFrameCommands.Count - 1];

        // we should have the wait command in last position otherwise we may have reached the end of the track ! (loop
        // command)
        if (!waitCom.isWait(false))
            return -1;
        // don't convert if we have a multi frame wait
        if (waitCom.getWaitFrame() > 1)
            return -1;

        bool[] hasKeyCh = new bool[6];
        J.Fill(hasKeyCh, false);

        // start from last command before wait
        for (int c = fmFrameCommands.Count - 2; c >= 0; c--)
        {
            XGMFMCommand com = fmFrameCommands[c];

            // can add wait and no key com found for this channel ?
            if (com.supportWait() && !hasKeyCh[com.getChannel()])
            {
                // add it
                com.addWait();
                // make wait command dummy
                waitCom.setDummy();
                // return index of current command
                return c;
            }
            // better to not swap command order then...
            else if (com.isYMKeyWrite())
                hasKeyCh[com.getChannel()] = true;
        }

        return -1;
    }

    public void optimizeFMCommands()
    {
        // 1. replace FM write by load instrument com
        // 2. replace FM write by set TL com
        // 3. replace FM write by panning com
        // 4. remove duplicate set ch freq in a single frame (keep last one)
        // 5. cleanup duplicate Key OFF/ON (start from end of frame)
        // 6. combine key ON/OFF with set FREQ com
        // 7. replace set FREQ with set FREQ low com when possible
        // 8. last pack key commands:
        // OFF followed by ON in same frame without FM write in between --> ON
        // ON followed by OFF in same frame without FM write in between --> OFF
        // 9. pack wait (should be last)

        if (Launcher.verbose)
        {
            int numKeyCom = 0;
            int numKeyAdvCom = 0;
            int numSetFreqCom = 0;
            int numFMSetCom = 0;
            int numWaitCom = 0;

            foreach (XGMFMCommand com in FMcommands)
            {
                if (com.isYMKeyFastWrite())
                    numKeyCom++;
                else if (com.isYMKeyAdvWrite())
                    numKeyAdvCom++;
                else if (com.isYMFreqWrite())
                    numSetFreqCom++;
                else if (com.isYMWrite())
                    numFMSetCom++;
                else if (com.isWait(true))
                    numWaitCom++;
            }

            if (Launcher.verbose)
            {
                Console.WriteLine("FM command stats before optimization:");
                Console.WriteLine("FM set = " + numFMSetCom + " - set freq = " + numSetFreqCom + " - key = " + numKeyCom + " - key adv = " + numKeyAdvCom
                        + " - wait = " + numWaitCom);
            }
        }

        List<XGMFMCommand> newCommands = new();
        YM2612State ymState = new YM2612State();

        JMap<int, List<XGMFMCommand>> commandsPerFrame = getFMCommandsPerFrame();
        List<int> frames = new(commandsPerFrame.Keys);
        // sort frames
        J.Sort(frames);

        // do frame optimization
        foreach (int frame in frames)
        {
            // get commands for this frame
            List<XGMFMCommand> frameCommands = commandsPerFrame[frame];

            for (int channel = 0; channel < 6; channel++)
            {
                int port = (channel >= 3) ? 1 : 0;
                int ch = (channel >= 3) ? channel - 3 : channel;

                // get commands for current channel
                List<XGMFMCommand> fmChannelCommands = XGMFMCommand.filterChannel(frameCommands, channel, false, false);

                int startInd = 0;
                while (startInd < fmChannelCommands.Count)
                {
                    // need to split on key command as envelop settings are updated on key on/off event
                    int endInd = XGMFMCommand.findNextYMKeyCommand(fmChannelCommands, startInd);

                    // get YMWrite commands for current channel
                    List<XGMFMCommand> fmWriteChannelCommands = XGMFMCommand.filterYMWrite(fmChannelCommands, startInd, endInd);

                    // FM write commands optimization
                    if (!fmWriteChannelCommands.IsEmptyJ())
                    {
                        // clear
                        newCommands.Clear();

                        // convert to load inst command
                        newCommands.AddRange(optimizeLoadInst(port, ch, fmWriteChannelCommands, ymState));
                        // convert to set TL
                        newCommands.AddRange(optimizeSetTL(port, ch, fmWriteChannelCommands));
                        // convert to set panning
                        newCommands.AddRange(optimizeSetPanning(port, ch, fmWriteChannelCommands, ymState));

                        // update YM state from remaining commands
                        ymState.updateState(fmWriteChannelCommands);

                        if (!newCommands.IsEmptyJ())
                        {
                            int index;

                            // get index of first command
                            index = FMcommands.IndexOf(fmWriteChannelCommands[0]);
                            // insert new commands here
                            FMcommands.AddRange(index, newCommands);

                            // get index of first command
                            index = fmChannelCommands.IndexOf(fmWriteChannelCommands[0]);
                            // insert new commands here
                            fmChannelCommands.AddRange(index, newCommands);
                            // need to adjust end index
                            endInd += newCommands.Count;

                            // update state from new commands
                            ymState.updateState(newCommands);
                        }
                    }

                    startInd = endInd + 1;
                }

                // FM commands optimization
                if (!fmChannelCommands.IsEmptyJ())
                {
                    // remove duplicate set freq
                    removeDuplicateSetFreq(fmChannelCommands);
                    // cleanup key commands
                    cleanKeyCommands(fmChannelCommands);
                    // combine key / set freq commands
                    combineSetFreqKeyCommands(fmChannelCommands);
                    // optimize key sequence commands
                    optimizeKeySeqCommands(fmChannelCommands);
                }
            }
        }

        // need to update time before processing global optimizations
        updateTimes();

        // do global optimizations
        for (int channel = 0; channel < 6; channel++)
        {
            // get commands for current channel (with wait commands as well)
            List<XGMFMCommand> fmChannelCommands = XGMFMCommand.filterChannel(FMcommands, channel, false, true);

            // pack FREQ to FREQ DELTA when possible
            packFMFreqCommands(fmChannelCommands);
            // convert set TL to delta TL when possible
            convertFMTLCommands(fmChannelCommands);
        }

        packWaitFM();

        // update commands per frame
        commandsPerFrame = getFMCommandsPerFrame();
        frames = new(commandsPerFrame.Keys);
        // sort frames
        J.Sort(frames);

        // do last frame optimization
        foreach (int frame in frames)
        {
            // get commands for this frame
            List<XGMFMCommand> frameCommands = commandsPerFrame[frame];

            // convert to ext wait commands when possible
            int indOpt = useExtWaitFMCommand(frameCommands);

            // optimized command is not the last frame command ?
            if ((indOpt != -1) && (indOpt != (frameCommands.Count - 2)))
            {
                XGMFMCommand comOpt = frameCommands[indOpt];
                int ind1 = FMcommands.IndexOf(comOpt);
                int ind2 = FMcommands.IndexOf(frameCommands[frameCommands.Count - 2]);

                // move to last command (don't swap)                
                FMcommands.Add(ind2 + 1, comOpt);
                FMcommands.remove(ind1);               
                // Collections.swap(FMcommands, ind1, ind2);
            }
        }

        // rebuild / cleaned list
        rebuildFMCommands();

        if (Launcher.verbose)
        {
            int numLoadInstCom = 0;
            int numTLCom = 0;
            int numTLDeltaCom = 0;
            int numPANCom = 0;
            int numSetFreqCom = 0;
            int numSetFreqDelta = 0;
            int numKeyCom = 0;
            int numKeySeqCom = 0;
            int numKeyAdvCom = 0;
            int numFMSetCom = 0;
            int numWaitCom = 0;

            foreach (XGMFMCommand com in FMcommands)
            {
                if (com.isYMLoadInst())
                    numLoadInstCom++;
                else if (com.isYMSetTL())
                    numTLCom++;
                else if (com.isYMTLDelta())
                    numTLDeltaCom++;
                else if (com.isYMPAN())
                    numPANCom++;
                else if (com.isYMFreqWrite())
                    numSetFreqCom++;
                else if (com.isYMFreqDeltaWrite())
                    numSetFreqDelta++;
                else if (com.isYMKeyFastWrite())
                    numKeyCom++;
                else if (com.isYMKeySequence())
                    numKeySeqCom++;
                else if (com.isYMKeyAdvWrite())
                    numKeyAdvCom++;
                else if (com.isYMWrite())
                    numFMSetCom++;
                else if (com.isWait(true))
                    numWaitCom++;
            }

            if (Launcher.verbose)
            {
                Console.WriteLine("Generated " + numLoadInstCom + " FM Load inst. commands");
                Console.WriteLine("Generated " + numTLCom + " FM set TL commands");
                Console.WriteLine("Generated " + numTLDeltaCom + " FM TL delta commands");
                Console.WriteLine("Generated " + numPANCom + " FM set PAN commands");
                Console.WriteLine("FM command stats after optimization:");
                Console.WriteLine("FM set = " + numFMSetCom + " - set freq = " + numSetFreqCom + " - set freq delta = " + numSetFreqDelta + " - key = "
                        + numKeyCom + " - key adv = " + numKeyAdvCom + " - key seq = " + numKeySeqCom + " - wait = " + numWaitCom);
            }
        }
    }

    public void rebuildFMCommands()
    {
        List<XGMFMCommand> newCommands = new();

        // rebuild / cleaned list
        int frameSize = 0;
        foreach (XGMFMCommand com in FMcommands)
        {
            if (!com.isDummy())
            {
                // above max frame size (take 1 extra byte for frame delay marker) ?
                if ((frameSize + com.size) > (XGCPacker.FRAME_MAX_SIZE - 1))
                {
                    Console.WriteLine("Warning: maximum frame size exceeded (FM frame #" + com.getFrame(pal) + ")");

                    // insert frame delay command (0xF0)
                    newCommands.Add(new XGMFMCommand.FrameDelayCommand());
                    // reset frame size
                    frameSize = 0;
                }
                else if (com.isWait(false))
                    // reset frame size
                    frameSize = 0;
                else
                    // increment frame size
                    frameSize += com.size;

                // add command
                newCommands.Add(com);
            }
        }

        FMcommands.Clear();
        FMcommands.AddRange(newCommands);

        updateOffsets();
        updateTimes();
    }

    public void rebuildPSGCommands()
    {
        List<XGMPSGCommand> newCommands = new();

        int frameSize = 0;
        foreach (XGMPSGCommand com in PSGcommands)
        {
            if (!com.isDummy())
            {
                // above max frame size (take 1 extra byte for frame delay marker) ?
                if ((frameSize + com.size) > (XGCPacker.FRAME_MAX_SIZE - 1))
                {
                    Console.WriteLine("Warning: maximum frame size exceeded (PSG frame #" + com.getFrame(pal) + ")");

                    // insert frame delay command (0xF0)
                    newCommands.Add(new XGMPSGCommand.FrameCommand());
                    // reset frame size
                    frameSize = 0;
                }
                else if (com.isWait(false))
                    // reset frame size
                    frameSize = 0;
                else
                    // increment frame size
                    frameSize += com.size;

                // add command
                newCommands.Add(com);
            }
        }

        PSGcommands.Clear();
        PSGcommands.AddRange(newCommands);

        updateOffsets();
        updateTimes();
    }

    public void removeDuplicatedFreqEnv(List<XGMPSGCommand> psgFrameChannelCommands)
    {
        // start from last command
        bool freqLowSet = false;
        bool freqSet = false;
        bool envSet = false;
        for (int c = psgFrameChannelCommands.Count - 1; c >= 0; c--)
        {
            XGMPSGCommand com = psgFrameChannelCommands[c];

            if (com.isEnv())
            {
                // ENV is already set in the frame --> set dummy
                if (envSet)
                    com.setDummy();
                else
                    envSet = true;
            }
            else if (com.isFreq())
            {
                // FREQ is already set in the frame --> set dummy
                if (freqSet)
                    com.setDummy();
                else
                    freqSet = true;
            }
            else if (com.isFreqLow())
            {
                // FREQ is already set in the frame --> set dummy
                if (freqLowSet || freqSet)
                    com.setDummy();
                else
                    freqLowSet = true;
            }
        }
    }

    public int useExtWaitPSGCommand(List<XGMPSGCommand> psgFrameCommands)
    {
        // no need to process
        if (psgFrameCommands.Count < 2)
            return -1;

        XGMPSGCommand waitCom = psgFrameCommands[psgFrameCommands.Count - 1];

        // we should have the wait command in last position otherwise we may have reached the end of the track ! (loop
        // command)
        if (!waitCom.isWait(false))
            return -1;
        // don't convert if we have a multi frame wait
        if (waitCom.getWaitFrame() > 1)
            return -1;

        // start from last command before wait
        for (int c = psgFrameCommands.Count - 2; c >= 0; c--)
        {
            XGMPSGCommand com = psgFrameCommands[c];

            // can add wait ?
            if (com.supportWait())
            {
                // add it
                com.addWait();
                // make wait command dummy
                waitCom.setDummy();
                // return index of current command
                return c;
            }
        }

        return -1;
    }

    public void removeSilentPSGFreqCommands(List<XGMPSGCommand> psgChannelCommands)
    {
        bool silent = false;
        XGMPSGCommand lastDummyFreqCom = null;
        XGMPSGCommand lastDummyFreqLowCom = null;
        int lastFreq = 0;

        foreach (XGMPSGCommand com in psgChannelCommands)
        {
            if (com.isDummy())
                continue;

            // loop start ? --> reset state
            if (com.isLoopStart())
            {
                silent = false;
                lastDummyFreqCom = null;
                lastDummyFreqLowCom = null;
            }
            else if (com.isEnv())
            {
                // changed to silent ?
                if (com.getEnv() == 0xF)
                {
                    silent = true;
                    // reset it
                    lastDummyFreqCom = null;
                    lastDummyFreqLowCom = null;
                }
                else
                {
                    // changed from silent to audible ?
                    if (silent)
                    {
                        // restore last remove setFreq command
                        if (lastDummyFreqCom != null)
                        {
                            lastDummyFreqCom.clearDummy();
                            lastDummyFreqCom.setFreq(lastFreq);
                        }
                        else if (lastDummyFreqLowCom != null)
                        {
                            lastDummyFreqLowCom.clearDummy();
                            lastDummyFreqLowCom.setFreqLow(lastFreq & 0xF);
                        }

                        silent = false;
                    }
                }
            }
            else if (com.isFreq())
            {
                // channel is currently silent ? --> set dummy
                if (silent)
                {
                    lastFreq = com.getFreq();
                    com.setDummy();
                    // store last removed setFreq com
                    lastDummyFreqCom = com;
                }
            }
            else if (com.isFreqLow())
            {
                // channel is currently silent ? --> set dummy
                if (silent)
                {
                    lastFreq = (lastFreq & 0x3F0) | com.getFreqLow();
                    com.setDummy();
                    // store last removed setFreq com
                    lastDummyFreqLowCom = com;
                }
            }
        }
    }

    public void packPSGFreqCommands(List<XGMPSGCommand> psgChannelCommands)
    {
        int lastFreq = -1;
        foreach (XGMPSGCommand com in psgChannelCommands)
        {
            if (com.isDummy())
                continue;

            // loop start ? --> reset state
            if (com.isLoopStart())
                lastFreq = -1;
            else if (com.isFreq())
            {
                int freq = com.getFreq();

                if (lastFreq != -1)
                {
                    // compute delta
                    int delta = freq - lastFreq;

                    // can happen with the 'removeSilentPSGFreqCommands' optimization pass
                    if (delta == 0)
                        com.setDummy();
                    else if (Math.Abs(delta) <= 4)
                        com.toFreqDelta(delta);
                    // only low part changed ? --> change to freq low (same size but faster processing and potentially
                    // compress better)
                    else if ((lastFreq & 0xFF0) == (freq & 0xFF0))
                        com.toFreqLow(freq & 0xF);
                }

                lastFreq = freq;
            }
            else if (com.isFreqLow())
            {
                if (lastFreq != -1)
                {
                    // new freq
                    int freq = (lastFreq & 0xFF0) | com.getFreqLow();
                    // compute delta
                    int delta = freq - lastFreq;

                    // can happen with the 'removeSilentPSGFreqCommands' optimization pass
                    if (delta == 0)
                        com.setDummy();
                    else if (Math.Abs(delta) <= 4)
                        com.toFreqDelta(delta);

                    lastFreq = freq;
                }
            }
        }
    }

    public void convertPSGEnvCommands(List<XGMPSGCommand> psgChannelCommands)
    {
        int lastEnv = -1;

        foreach (XGMPSGCommand com in psgChannelCommands)
        {
            if (com.isDummy())
                continue;

            // loop start ? --> reset state
            if (com.isLoopStart())
                lastEnv = -1;
            else if (com.isEnv())
            {
                int env = com.getEnv();

                if (lastEnv != -1)
                {
                    // compute delta
                    int delta = env - lastEnv;

                    if (delta == 0)
                    {
                        if (Launcher.verbose)
                            Console.WriteLine("env delta = 0");
                        com.setDummy();
                    }
                    else if (Math.Abs(delta) <= 4)
                        com.toEnvDelta(delta);
                }

                lastEnv = env;
            }
        }
    }

    public void optimizePSGCommands()
    {
        // NEW: add this for better optimization
        // 1. remove duplicate set FREQ and set ENV commands per frame / channel
        // 2. remove all FREQ commands when ENV keep silent
        // 3. replace FREQ by FREQ_LOW when possible
        // 5. pack wait
        // 4. replace ENV/FREQ/FREQ_LOW with .WAIT extension when possible

        JMap<int, List<XGMPSGCommand>> commandsPerFrame = getPSGCommandsPerFrame();
        List<int> frames = new(commandsPerFrame.Keys);
        // sort frames
        J.Sort(frames);

        // do frame optimization
        foreach (int frame in frames)
        {
            // get commands for this frame
            List<XGMPSGCommand> frameCommands = commandsPerFrame[frame];

            for (int channel = 0; channel < 4; channel++)
            {
                // get commands for current channel (with wait commands as well)
                List<XGMPSGCommand> psgFrameChannelCommands = XGMPSGCommand.filterChannel(frameCommands, channel, false, false);

                // remove duplicate
                removeDuplicatedFreqEnv(psgFrameChannelCommands);
            }
        }

        // do global optimizations
        for (int channel = 0; channel < 4; channel++)
        {
            // get commands for current channel (with wait commands as well)
            List<XGMPSGCommand> psgChannelCommands = XGMPSGCommand.filterChannel(PSGcommands, channel, true, true);

            // remove inaudible set FREQ command (only for channel 0-1)
            if (channel < 2)
                removeSilentPSGFreqCommands(psgChannelCommands);
            // pack FREQ to FREQ DELTA / LOW when possible
            packPSGFreqCommands(psgChannelCommands);
            // convert ENV to ENV DELTA when possible
            convertPSGEnvCommands(psgChannelCommands);
        }

        if (Launcher.verbose)
        {
            int numEnv = 0;
            int numFreq = 0;
            int numWaitCom = 0;
            int converted = 0;

            foreach (XGMPSGCommand com in PSGcommands)
            {
                if (com.isDummy())
                    continue;

                if (com.isFreq())
                    numFreq++;
                else if (com.isFreqLow())
                    numFreq++;
                else if (com.isFreqDelta())
                    converted++;
                else if (com.isEnv())
                    numEnv++;
                else if (com.isWait(true))
                    numWaitCom++;
            }

            if (Launcher.verbose)
            {
                Console.WriteLine("PSG command stats: set tone = " + (numFreq + converted) + " - set env = " + numEnv);
                Console.WriteLine("Packed " + converted + " PSG set tone commands");
                Console.WriteLine("PSG wait command before optimization = " + numWaitCom);
            }
        }

        // build cleaned list (needed for proper wait packing)
        List<XGMPSGCommand> newCommands = new();
        foreach (XGMPSGCommand com in PSGcommands)
        {
            if (!com.isDummy())
                newCommands.Add(com);
        }

        PSGcommands.Clear();
        PSGcommands.AddRange(newCommands);

        // pack wait
        packWaitPSG();

        // update commands per frame
        commandsPerFrame = getPSGCommandsPerFrame();
        frames = new(commandsPerFrame.Keys);
        // sort frames
        J.Sort(frames);

        // do last frame optimization
        foreach (int frame in frames)
        {
            // get commands for this frame
            List<XGMPSGCommand> frameCommands = commandsPerFrame[frame];

            // convert to ext wait commands when possible
            int indOpt = useExtWaitPSGCommand(frameCommands);

            // optimized command is not the last frame command ?
            if ((indOpt != -1) && (indOpt != (frameCommands.Count - 2)))
            {
                // swap with last command
                int ind1 = PSGcommands.IndexOf(frameCommands[indOpt]);
                int ind2 = PSGcommands.IndexOf(frameCommands[frameCommands.Count - 2]);
                (PSGcommands[ind1], PSGcommands[ind2]) = (PSGcommands[ind2], PSGcommands[ind1]);
            }
        }

        if (Launcher.verbose)
        {
            int numWaitCom = 0;

            foreach (XGMPSGCommand com in PSGcommands)
            {
                if (com.isWait(true))
                    numWaitCom++;
            }

            Console.WriteLine("PSG wait command after optimization = " + numWaitCom);
        }

        // rebuild / cleaned list
        rebuildPSGCommands();
    }

    public void optimizeCommands()
    {
        optimizeFMCommands();
        optimizePSGCommands();
    }

    public void optimizeSamples()
    {
        int numSample = samples.Count;

        // start from end as we delete merged samples
        for (int s = samples.Count - 1; s >= 0; s--)
            mergeSample(s);

        // reset sample ids
        for (int s = 0; s < samples.Count; s++)
        {
            XGMSample sample = samples[s];
            updateSampleCommands(sample.id, s + 1, -1);
            sample.id = s + 1;
        }

        // maximum number of sample reached ?
        if (samples.Count >= (124 - 1))
        {
            Console.Error.WriteLine("Warning: XGM cannot have more than 123 samples, some samples will be lost !");

            // remove extra samples
            while (samples.Count > 123)
                samples.remove(samples.Count - 1);

            return;
        }

        rebuildFMCommands();

        if ((samples.Count != numSample) && !Launcher.silent)
            Console.WriteLine("Merged " + (numSample - samples.Count) + " sample(s)");
    }

    public XGMSample findMatchingSample(XGMSample sample)
    {
        XGMSample bestMatch = null;
        double bestScore = 0d;

        foreach (XGMSample s in samples)
        {
            if (s != sample)
            {
                double score = s.getSimilarityScore(sample);
                if (score > bestScore)
                {
                    bestMatch = s;
                    bestScore = score;
                }
            }
        }

        // accept only if score >= 1
        if (bestScore >= 1)
            return bestMatch;

        return null;
    }

    public void mergeSample(int sampleIndex)
    {
        XGMSample sample = samples[sampleIndex];
        // find if already exist
        XGMSample matchingSample = findMatchingSample(sample);

        // we found a duplicate ?
        if (matchingSample != null)
        {
            bool sameDuration = J.Round(sample.getLength() / 60) == J.Round(matchingSample.getLength() / 60);
            // update VGM so it now uses the matching sample id
            updateSampleCommands(sample.id, matchingSample.id, sameDuration ? -1 : (sample.getLength() * 44100L) / XGMSample.XGM_FULL_RATE);
            // remove duplicate from samples
            samples.remove(sampleIndex);

            // if (Launcher.verbose)
            Console.WriteLine("Found duplicated sample #" + sample.id + " (merged with #" + matchingSample.id + ")");
        }
    }

    public static List<XGMFMCommand> compileYMCommands(JMap<int, List<VGMCommand>> ymChannelSetCommands, JMap<int, int> ymFreqCommands,
            List<VGMCommand> ymMiscCommands, JMap<int, VGMCommand> ymKeyCommands)
    {
        List<XGMFMCommand> result = new();

        // channel set YM commands first (sorted by channel)
        foreach (KeyValuePair<int, List<VGMCommand>> entry in ymChannelSetCommands)
            result.AddRange(XGMFMCommand.createYMCHCommands(entry.Value, entry.Key));
        // then frequency YM command (sorted by channel)
        foreach (KeyValuePair<int, int> entry in ymFreqCommands)
        {
            int ch = entry.Key;
            result.Add(XGMFMCommand.createYMFreqCommand(ch & 7, (ch & 8) != 0, entry.Value, false, false));
        }
        // then YM misc commands
        if (!ymMiscCommands.IsEmptyJ())
            result.AddRange(XGMFMCommand.createYMMiscCommands(ymMiscCommands));
        // then key commands
        result.AddRange(XGMFMCommand.createYMKeyCommands(ymKeyCommands.Values));

        // done
        ymChannelSetCommands.Clear();
        ymFreqCommands.Clear();
        ymMiscCommands.Clear();
        ymKeyCommands.Clear();

        return result;
    }

    /**
     * Find the FM LOOP Start command index
     */
    public int getFMLoopStartCommandIndex()
    {
        for (int i = 0; i < FMcommands.Count; i++)
            if (FMcommands[i].isLoopStart())
                return i;

        return -1;
    }

    /**
     * Find the FM LOOP Start command
     */
    public XGMFMCommand getFMLoopStartCommand()
    {
        int index = getFMLoopStartCommandIndex();

        if (index != -1)
            return FMcommands[index];

        return null;
    }

    /**
     * Find the FM LOOP command
     */
    public XGMFMCommand getFMLoopCommand()
    {
        foreach (XGMFMCommand command in FMcommands)
            if (command.isLoop())
                return command;

        return null;
    }

    // /**
    // * Return the position of the command pointed by the loop
    // */
    // public int getFMLoopPointedCommandIndex()
    // {
    // LoopCommand loopCommand = (LoopCommand) getFMLoopCommand();
    //
    // if (loopCommand != null)
    // return getFMCommandIndexAtOffset(loopCommand.getLoopAddr());
    //
    // return -1;
    // }
    //
    // /**
    // * Return the command pointed by the loop
    // */
    // public XGMFMCommand getFMLoopPointedCommand()
    // {
    // int index = getFMLoopPointedCommandIndex();
    //
    // if (index != -1)
    // return FMcommands[index];
    //
    // return null;
    // }

    /**
     * Find the PSG LOOP Start command index
     */
    public int getPSGLoopStartCommandIndex()
    {
        for (int i = 0; i < PSGcommands.Count; i++)
            if (PSGcommands[i].isLoopStart())
                return i;

        return -1;
    }

    /**
     * Find the PSG LOOP Start command
     */
    public XGMPSGCommand getPSGLoopStartCommand()
    {
        int index = getPSGLoopStartCommandIndex();

        if (index != -1)
            return PSGcommands[index];

        return null;
    }

    /**
     * Find the PSG LOOP command
     */
    public XGMPSGCommand getPSGLoopCommand()
    {
        foreach (XGMPSGCommand command in PSGcommands)
            if (command.isLoop())
                return command;

        return null;
    }

    /**
     * Update time information for all command
     */
    public void updateTimes()
    {
        int time;

        time = 0;
        foreach (XGMFMCommand com in FMcommands)
        {
            com.time = time;

            if (pal)
                time += com.getWaitFrame() * 882;
            else
                time += com.getWaitFrame() * 735;
        }
        time = 0;
        foreach (XGMPSGCommand com in PSGcommands)
        {
            com.time = time;

            if (pal)
                time += com.getWaitFrame() * 882;
            else
                time += com.getWaitFrame() * 735;
        }
    }

    /**
     * Update origin offset information for all command
     */
    public void updateOffsets()
    {
        Command.computeOffsets(FMcommands, 0);
        Command.computeOffsets(PSGcommands, 0);
    }

    public int getTotalTime()
    {
        int size = FMcommands.Count;
        if (size == 0)
            return 0;

        return FMcommands[size - 1].time;
    }

    public int getTotalTimeInFrame()
    {
        return getTotalTime() / (pal ? 882 : 735);
    }

    public int getTotalTimeInSecond()
    {
        return getTotalTimeInFrame() / (pal ? 50 : 60);
    }

    public int getTimeFrom(VGMCommand from)
    {
        return getTotalTime() - from.time;
    }

    public int getLoopDurationInSecond()
    {
        return getLoopDurationInFrame() / (pal ? 50 : 60);
    }

    public int getLoopDurationInFrame()
    {
        XGMFMCommand loopStartCom = getFMLoopStartCommand();
        return (loopStartCom != null) ? (getTotalTime() - loopStartCom.time) / (pal ? 882 : 735) : 0;
    }

    /**
     * Return elapsed time when specified command happen
     */
    public int getFMCommandIndexAtTime(int time)
    {
        return Command.getCommandIndexAtTime(FMcommands, time);
    }

    public int getFMCommandIndexAtOffset(int offset)
    {
        return Command.getCommandIndexAtOffset(FMcommands, offset);
    }

    public XGMFMCommand getFMCommandAtOffset(int offset)
    {
        return (XGMFMCommand) Command.getCommandAtOffset(FMcommands, offset);
    }

    /**
     * Return elapsed time when specified command happen
     */
    public XGMFMCommand getFMCommandAtTime(int time)
    {
        return (XGMFMCommand) Command.getCommandAtTime(FMcommands, time);
    }

    public XGMSample getSample(int id)
    {
        foreach (XGMSample sample in samples)
            if (sample.id == id)
                return sample;

        return null;
    }

    public int getSampleLen(int id)
    {
        XGMSample sample = getSample(id);
        if (sample != null)
            return sample.getLength();

        return 0;
    }

    public XGMSample getSampleByOriginId(int id)
    {
        foreach (XGMSample sample in samples)
            if (sample.originId == id)
                return sample;

        return null;
    }

    public XGMSample getSampleByOriginAddress(int addr)
    {
        foreach (XGMSample sample in samples)
            if (sample.originAddr == addr)
                return sample;

        return null;
    }

    public List<int> getLoopSplittedFMMusicFrameOffsets(bool beforeLoop)
    {
        List<int> result = new();
        int loopStartComIndex = getFMLoopStartCommandIndex();
        int loopInd = (loopStartComIndex == -1) ? FMcommands.Count : loopStartComIndex;

        if (beforeLoop)
        {
            for (int i = 0; i < loopInd; i++)
            {
                XGMFMCommand command = FMcommands[i];

                if (command.isWait(false) || command.isLoop() || command.isFrameDelay())
                    result.Add((int)(command.getOriginOffset() + command.size));
            }
        }
        else
        {
            int baseOffset = (loopStartComIndex != -1) ? FMcommands[loopStartComIndex].getOriginOffset() : 0;

            for (int i = loopInd; i < FMcommands.Count; i++)
            {
                XGMFMCommand command = FMcommands[i];

                if (command.isWait(false) || command.isLoop() || command.isFrameDelay())
                    result.Add((int)((command.getOriginOffset() + command.size) - baseOffset));
            }
        }

        return result;
    }

    public List<int> getLoopSplittedPSGMusicFrameOffsets(bool beforeLoop)
    {
        List<int> result = new();
        int loopStartComIndex = getPSGLoopStartCommandIndex();
        int loopInd = (loopStartComIndex == -1) ? PSGcommands.Count : loopStartComIndex;

        if (beforeLoop)
        {
            for (int i = 0; i < loopInd; i++)
            {
                XGMPSGCommand command = PSGcommands[i];

                if (command.isWait(false) || command.isLoop())
                    result.Add((int)(command.getOriginOffset() + command.size));
            }
        }
        else
        {
            int baseOffset = (loopStartComIndex != -1) ? PSGcommands[loopStartComIndex].getOriginOffset() : 0;

            for (int i = loopInd; i < PSGcommands.Count; i++)
            {
                XGMPSGCommand command = PSGcommands[i];

                if (command.isWait(false) || command.isLoop())
                    result.Add((int)((command.getOriginOffset() + command.size) - baseOffset));
            }
        }

        return result;
    }

    public List<int> getFMMusicFrameOffsets()
    {
        List<int> result = new();

        foreach (XGMFMCommand command in FMcommands)
        {
            if (command.isWait(false) || command.isLoop())
                result.Add((int)(command.getOriginOffset() + command.size));
        }

        return result;
    }

    public List<int> getPSGMusicFrameOffsets()
    {
        List<int> result = new();

        foreach (XGMPSGCommand command in PSGcommands)
        {
            if (command.isWait(false) || command.isLoop())
                result.Add((int)(command.getOriginOffset() + command.size));
        }

        return result;
    }

    public byte[] getFMMusicDataArray()
    {
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        foreach (XGMFMCommand command in FMcommands)
            result.write(command.data);

        return result.toByteArray();
    }

    public byte[] getPSGMusicDataArray()
    {
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        foreach (XGMPSGCommand command in PSGcommands)
            result.write(command.data);

        return result.toByteArray();
    }

    public byte[] getFMMusicDataArray(bool beforeLoop)
    {
        int loopStartComIndex = getFMLoopStartCommandIndex();
        int loopInd = (loopStartComIndex == -1) ? FMcommands.Count : loopStartComIndex;
        ByteArrayOutputStream result = new ByteArrayOutputStream();
        int ind;

        if (beforeLoop)
        {
            ind = 0;
            // build data array for no looping sequence
            while (ind < loopInd)
                result.write(FMcommands[ind++].data);
        }
        else
        {
            ind = loopInd;
            // build data array for looping sequence
            while (ind < FMcommands.Count)
                result.write(FMcommands[ind++].data);
        }

        return result.toByteArray();
    }

    public byte[] getPSGMusicDataArray(bool beforeLoop)
    {
        int loopStartComIndex = getPSGLoopStartCommandIndex();
        int loopInd = (loopStartComIndex == -1) ? PSGcommands.Count : loopStartComIndex;
        ByteArrayOutputStream result = new ByteArrayOutputStream();
        int ind;

        if (beforeLoop)
        {
            ind = 0;
            // build data array for no looping sequence
            while (ind < loopInd)
                result.write(PSGcommands[ind++].data);
        }
        else
        {
            ind = loopInd;
            // build data array for looping sequence
            while (ind < PSGcommands.Count)
                result.write(PSGcommands[ind++].data);
        }

        return result.toByteArray();
    }

    public byte[] getPackedFMMusicDataArray()
    {
        // we need to split data block on start loop position
        // so we can properly restore loop offset after packing operation
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        // pack first part of data
        result.write(XGCPacker.pack(getFMMusicDataArray(true), getLoopSplittedFMMusicFrameOffsets(true), 0));
        // do we have a loop section --> set loop offset
        if (getFMLoopStartCommandIndex() != -1)
            setFMLoopAddress(result.size());
        // -1 = no loop
        else
            setFMLoopAddress(-1);
        // pack second part of data
        result.write(XGCPacker.pack(getFMMusicDataArray(false), getLoopSplittedFMMusicFrameOffsets(false), result.size()));

        return result.toByteArray();
    }

    public byte[] getPackedPSGMusicDataArray()
    {
        // we need to split data block on start loop position
        // so we can properly restore loop offset after packing operation
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        // pack first part of data
        result.write(XGCPacker.pack(getPSGMusicDataArray(true), getLoopSplittedPSGMusicFrameOffsets(true), 0));
        // do we have a loop section --> set loop offset
        if (getPSGLoopStartCommandIndex() != -1)
            setPSGLoopAddress(result.size());
        // -1 = no loop
        else
            setPSGLoopAddress(-1);
        // pack second part of data
        result.write(XGCPacker.pack(getPSGMusicDataArray(false), getLoopSplittedPSGMusicFrameOffsets(false), result.size()));

        return result.toByteArray();
    }

    public int getTotalMusicDataSize()
    {
        return getFMMusicDataSize() + getPSGMusicDataSize();
    }

    public int getFMMusicDataSize()
    {
        int result = 0;

        foreach (XGMFMCommand command in FMcommands)
            result += command.size;

        return result;
    }

    public int getPSGMusicDataSize()
    {
        int result = 0;

        foreach (XGMPSGCommand command in PSGcommands)
            result += command.size;

        return result;
    }

    public int getPackedFMMusicDataSize()
    {
        return getPackedFMMusicDataArray().Length;
    }

    public int getPackedPSGMusicDataSize()
    {
        return getPackedPSGMusicDataArray().Length;
    }

    public byte[] getPCMDataArray()
    {
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        for (int s = 0; s < samples.Count; s++)
        {
            byte[] copy = (byte[])samples[s].data.Clone();

            // sign the sample
            for (int i = 0; i < copy.Length; i++)
                copy[i] += 0x80;

            result.write(copy);
        }

        return result.toByteArray();
    }

    public int getPCMDataSize()
    {
        int result = 0;

        foreach (XGMSample sample in samples)
            result += sample.data.Length;

        return result;
    }

    public byte[] asByteArray()
    {
        int offset;
        int data;
        int len;
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        // 0000: XGM2 id (ignored when compiled in ROM)
        if (!packed)
            result.write(Util.getBytes("XGM2"));
        // 0004: version (0x10 currently)
        result.write(0x10);

        // 0005: format description (see xgm2.txt)
        data = 0;
        // bit #0: NTSC / PAL information: 0=NTSC 1=PAL
        if (pal)
            data |= 1;
        // bit #1: multi tracks file: 0=No 1=Yes (always 0 here)
        // bit #2: GD3 tags: 0=No 1=Yes
        if (gd3 != null)
            data |= 4;
        // bit #3: packed FM / PSG / GD3 data blocks: 0=No 1=Yes
        if (packed)
            data |= 8;
        // write format
        result.write(data);

        // get FM and PSG data blocks
        byte[] pcmData = getPCMDataArray();
        byte[] fmData;
        byte[] psgData;

        if (packed)
        {
            fmData = getPackedFMMusicDataArray();
            psgData = getPackedPSGMusicDataArray();
        }
        else
        {
            fmData = getFMMusicDataArray();
            psgData = getPSGMusicDataArray();
        }

        // align on 256 bytes
        pcmData = Util.align(pcmData, 256, 0);
        fmData = Util.align(fmData, 256, 0);
        psgData = Util.align(psgData, 256, 0);

        // 0006-0007: SLEN = Sample data bloc size / 256 (ex: $0200 means 512*256 = 131072 bytes)
        data = pcmData.Length >> 8;
        result.write(data >> 0);
        result.write(data >> 8);
        // 0008-0009: FMLEN = FM music data block size / 256 (ex: $0040 means 64*256 = 16384 bytes)
        data = fmData.Length >> 8;
        result.write(data >> 0);
        result.write(data >> 8);
        // 000A-000B: PSGLEN = PSG music data block size / 256 (ex: $0020 means 32*256 = 8192 bytes)
        data = psgData.Length >> 8;
        result.write(data >> 0);
        result.write(data >> 8);

        // 000C-0103: SID (sample id) table
        // size = 256-8 = 248 bytes so end of table will align on 256 bytes in ROM
        offset = 0;
        for (int s = 0; s < samples.Count; s++)
        {
            XGMSample sample = samples[s];
            len = sample.data.Length;

            // each entry of the table consist of 2 bytes for the address:
            // entry+$0: sample address / 256
            result.write(offset >> 8);
            result.write(offset >> 16);
            offset += len;
        }
        // required to get last sample size
        result.write(offset >> 8);
        result.write(offset >> 16);
        // fill with silent mark
        for (int s = samples.Count + 1; s < (248 / 2); s++)
        {
            result.write(0xFF);
            result.write(0xFF);
        }

        // 0104-xx04: sample data (see SLEN field for size)
        result.write(pcmData);
        // xx04-xx04: FM music data
        result.write(fmData);
        // xx04-xx04: PSG music data
        result.write(psgData);

        // xx04-xx04: GD3/XD3 data
        if (packed)
        {
            if (xd3 != null)
                result.write(xd3.asByteArray());
        }
        else if (gd3 != null)
            result.write(gd3.asByteArray());

        return result.toByteArray();
    }

    public void updateSampleCommands(int originId, int replaceId, long durationInSample)
    {
        updateSampleCommands(originId, replaceId, durationInSample, null);
    }

    public void updateSampleCommands(int originId, int replaceId, long durationInSample, HashSet<XGMFMCommand> updatedCommands)
    {
        // nothing to do..
        if ((originId == replaceId) && (durationInSample == -1))
            return;

        int i = 0;
        while (i < FMcommands.Count)
        {
            XGMFMCommand command = FMcommands[i++];

            // found a command for the original sample ?
            if (command.isPCM() && (command.getPCMId() == originId) && ((updatedCommands == null) || !updatedCommands.Contains(command)))
            {
                // Do not reinterpret a shared sample id as a local id during another merge.
                if (updatedCommands != null)
                    updatedCommands.Add(command);

                // --> set new sample id
                command.setPCMId(replaceId);

                // we have to change duration ?
                if (durationInSample != -1)
                {
                    // get duration
                    long duration = durationInSample * (command.getPCMHalfRate() ? 2 : 1);
                    long endTime = command.time + duration;
                    long endTimeFrame = -1;
                    bool addStop = false;

                    // find position where to add stop command
                    while (i < FMcommands.Count)
                    {
                        XGMFMCommand tmpCom = FMcommands[i++];

                        // find another PCM command in between ? no need to stop then
                        if (tmpCom.isPCM())
                        {
                            i--;
                            break;
                        }
                        // duration reach ?
                        if (tmpCom.time >= endTime)
                        {
                            if (endTimeFrame == -1) endTimeFrame = tmpCom.time;
                            // next frame ?
                            else if (tmpCom.time > endTimeFrame)
                            {
                                // need to add stop PCM command
                                addStop = true;
                                i--;
                                break;
                            }
                        }
                    }

                    if (addStop)
                    {
                        FMcommands.Add(i - 1, XGMFMCommand.createPCMStopCommand(command.getPCMChannel()));
                        i++;
                    }
                }
            }
        }
    }
}
