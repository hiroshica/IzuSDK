using System.Text;
namespace Isdk.Xgm2Tool;




/**
 * Sega Megadrive VGM file decoder
 * 
 * @author Stephane Dallongeville
 */
public class VGM
{
    public readonly int SAMPLE_END_DELAY = 400;
    public readonly int SAMPLE_MIN_SIZE = 100;
    public readonly int SAMPLE_MIN_DYNAMIC = 16;
    public readonly int SAMPLE_ALLOWED_MARGE = 64;
    public readonly double SAMPLE_MIN_MEAN_DELTA = 1d;

    public byte[] data;

    public List<SampleBank> sampleBanks;
    public List<VGMCommand> commands;

    public int version;

    public int offsetStart;
    public int offsetEnd;
    public int lenInSample;

    public int loopStart;
    public int loopLenInSample;

    public int rate;
    public GD3 gd3;

    public VGM(byte[] data, bool convert)
    {
        if (!System.Text.Encoding.Latin1.GetString(data, 0x00, 4).EqualsIgnoreCaseJ("VGM "))
            throw new ArgumentException("File format not recognized !");

        // just check for sub version info (need version 1.50 at least)
        version = data[8] & 0xFF;
        if (version < 0x50)
            Console.WriteLine("Warning: VGM version 1." + J.Format("%2X", (int)(version)) + " detected: 1.5 required for PCM data !");

        if (!Launcher.silent)
        {
            if (convert)
                Console.WriteLine("Optimizing VGM...");
            else
                Console.WriteLine("Parsing VGM file...");
        }

        this.data = data;

        // start offset
        if (version >= 0x50)
            offsetStart = Util.getInt32(data, 0x34) + 0x34;
        else
            offsetStart = 0x40;
        // end offset
        offsetEnd = Util.getInt32(data, 0x04) + 0x04;

        // track len (in number of sample = 1/44100 sec)
        lenInSample = Util.getInt32(data, 0x18);

        // loop start offset
        loopStart = Util.getInt32(data, 0x1C);
        if (loopStart != 0)
            loopStart += 0x1C;
        // loop len (in number of sample = 1/44100 sec)
        loopLenInSample = Util.getInt32(data, 0x20);

        // 50 or 60 Hz
        if (version >= 0x01)
        {
            rate = Util.getInt32(data, 0x24);
            // not 50 ? --> then assume 60Hz / NTSC by default
            if (rate != 50)
                rate = 60;
        }
        else
            // assume NTSC by default
            rate = 60;

        // GD3 tags
        int addr = Util.getInt32(data, 0x14);
        // has GD3 tags ?
        if (addr != 0)
        {
            // transform to absolute address
            addr += 0x14;
            // and get GD3 infos
            gd3 = new GD3(data, addr);
        }
        else
            gd3 = null;

        if (!Launcher.silent)
            Console.WriteLine(J.Format("VGM lenght: %d (%d seconds)", (int)(lenInSample), (int)(lenInSample / 44100)));

        if (Launcher.verbose)
        {
            Console.WriteLine(J.Format("VGM data start: %6X   end: %6X", (int)(offsetStart), (int)(offsetEnd)));
            Console.WriteLine(J.Format("Loop start offset: %6X   lenght: %d (%d seconds)", (int)(loopStart), (int)(loopLenInSample),
                    (int)(loopLenInSample / 44100)));
        }

        sampleBanks = new();
        commands = new();

        // build command list
        parse();

        // update time and offsets for all commands
        updateTimes();
        updateOffsets();

        // and build samples
        buildSamples(convert);

        // update time and offsets for all commands
        updateTimes();
        updateOffsets();

        if (convert)
        {
            convertWaits();
            cleanCommands();
            cleanSamples();

            // need to be done here
            updateTimes();
            updateOffsets();

            fixKeyCommands();
            removeDummyStreamStopCommands();
            packWait();

            // update time and offsets for all commands
            updateTimes();
            updateOffsets();
        }

        if (!Launcher.silent)
        {
            Console.WriteLine(
                    J.Format("Computed VGM duration: %d samples (%d seconds)", (int)(getTotalTime()), (int)(getTotalTime() / 44100)));
        }
        if (Launcher.verbose)
        {
            Console.WriteLine("VGM sample number: " + getSampleNumber());
            Console.WriteLine("Sample data size: " + getSampleDataSize());
            Console.WriteLine("Sample total len: " + getSampleTotalLen());
        }
    }

    public VGM(VGM vgm, bool convert) : this(vgm.data, convert)
    {
    }

    public VGM(XGM xgm)
    {
        JMap<int, int> sampleAddr = new();
        byte[][] ymState = J.Jagged<byte>(2, 0x100);
        // 0 = env, 1 = freq
        short[][] psgState = J.Jagged<short>(2, 4);
        List<Command> mixedCommands = new();

        if (!Launcher.silent)
            Console.WriteLine("Converting XGM to VGM...");

        // add FM and PSG commands
        mixedCommands.AddRange(xgm.FMcommands);
        mixedCommands.AddRange(xgm.PSGcommands);

        // sort on time
        J.Sort(mixedCommands, Command.timeComparator);

        data = null;
        sampleBanks = new();
        commands = new();

        version = 0x60;
        offsetStart = 0;
        offsetEnd = 0;
        lenInSample = 0;
        loopStart = 0;
        loopLenInSample = 0;

        // PAL flag
        if (xgm.pal)
            rate = 50;
        else
            rate = 60;
        // GD3 tags
        if (xgm.gd3 != null)
            gd3 = xgm.gd3;
        else if (xgm.xd3 != null)
            gd3 = new GD3(xgm.xd3);
        else
            gd3 = null;

        // build sample data block / stream declaration commands
        if (!xgm.samples.IsEmptyJ())
        {
            byte[] comData;
            int pcmDataSize = xgm.getPCMDataSize();

            // build data block command
            comData = new byte[pcmDataSize + 7];
            comData[0] = 0x67;
            comData[1] = 0x66;
            comData[2] = 0x00;
            Util.setInt32(comData, 3, pcmDataSize);

            // copy sample data and build address map
            int off = 0;
            foreach (XGMSample sample in xgm.samples)
            {
                int len = sample.getLength();
                Array.Copy(sample.data, 0, comData, 7 + off, len);
                sampleAddr.put((int)(sample.id), (int)(off));
                off += len;
            }

            // create data block command
            VGMCommand command = new VGMCommand(comData);
            // add data block command
            commands.Add(command);

            // add single stream control / data (id = 1)
            commands.Add(new VGMCommand(new byte[] {(byte) VGMCommand.STREAM_CONTROL, 0x00, 0x02, 0x00, 0x2A}));
            commands.Add(new VGMCommand(new byte[] {(byte) VGMCommand.STREAM_DATA, 0x00, 0x00, 0x01, 0x00}));

            // add data block
            SampleBank bank = addDataBlock(command);
            // then add samples
            foreach (XGMSample sample in xgm.samples)
                bank.addSample(sampleAddr.get((int)(sample.id)), sample.getLength(), XGMSample.XGM_FULL_RATE);
        }

        int time = 0;
        int loopOffset = -1;
        foreach (Command command in mixedCommands)
        {
            int comsize;
            int port;
            int ch;
            int reg;
            byte value;
            int lvalue;
            int addr;
            int len;
            int id;

            if (command is XGMFMCommand)
            {
                XGMFMCommand fmCommand = (XGMFMCommand) command;

                while (time < fmCommand.time)
                {
                    if (rate == 50)
                    {
                        commands.Add(new VGMCommand(new byte[] {(byte) 0x63}));
                        time += 882;
                    }
                    else
                    {
                        commands.Add(new VGMCommand(new byte[] {(byte) 0x62}));
                        time += 735;
                    }
                }

                port = fmCommand.getYMPort();
                ch = fmCommand.getYMChannel();

                switch (fmCommand.getType())
                {
                    // we use the FM loop command to rebuild loop on VGM
                    case XGMFMCommand.LOOP:
                        loopOffset = ((XGMFMCommand.LoopCommand) command).getLoopAddr();
                        // 0xFFFFFF --> no loop
                        if (loopOffset == 0xFFFFFF)
                            loopOffset = -1;
                        break;

                    case XGMFMCommand.PCM:
                        id = fmCommand.getPCMId();
                        // stop command
                        if (id == 0)
                            commands.Add(new VGMCommand(new byte[] {(byte) VGMCommand.STREAM_STOP, 0x00}));
                        else
                        {
                            XGMSample sample = xgm.getSample(id);

                            // get sample address
                            addr = sampleAddr.get((int)(id));
                            len = sample.getLength();
                            // sample rate
                            reg = fmCommand.getPCMHalfRate() ? XGMSample.XGM_HALF_RATE : XGMSample.XGM_FULL_RATE;

                            commands.Add(new VGMCommand(
                                    new byte[] {(byte) VGMCommand.STREAM_FREQUENCY, 0x00, (byte) ((reg >> 0) & 0xFF), (byte) ((reg >> 8) & 0xFF), 0x00, 0x00}));
                            commands.Add(new VGMCommand(new byte[] {(byte) VGMCommand.STREAM_START_LONG, 0x00, (byte) ((addr >> 0) & 0xFF),
                                    (byte) ((addr >> 8) & 0xFF), (byte) ((addr >> 16) & 0xFF), 0x00, 0x01, (byte) ((len >> 0) & 0xFF),
                                    (byte) ((len >> 8) & 0xFF), (byte) ((len >> 16) & 0xFF), 0x00}));
                        }
                        break;

                    case XGMFMCommand.FM_LOAD_INST:
                        int d = 1;
                        // slot writes
                        for (int r = 0; r < 7; r++)
                        {
                            for (int s = 0; s < 4; s++)
                            {
                                reg = 0x30 + (r << 4) + (s << 2) + ch;
                                value = fmCommand.data[d++];
                                // save state
                                ymState[port][reg] = value;
                                // create command
                                commands.Add(new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) reg, value}));
                            }
                        }

                        // ch writes
                        reg = 0xB0 + ch;
                        value = fmCommand.data[d++];
                        // save state
                        ymState[port][reg] = value;
                        // create command
                        commands.Add(new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) reg, value}));
                        reg = 0xB4 + ch;
                        value = fmCommand.data[d];
                        // save state
                        ymState[port][0xB4 + ch] = value;
                        // create command
                        commands.Add(new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) reg, value}));
                        break;

                    case XGMFMCommand.FM_WRITE:
                        comsize = fmCommand.getYMNumWrite();

                        for (int j = 0; j < comsize; j++)
                        {
                            reg = Util.getInt8(fmCommand.data, (j * 2) + 1);
                            value = fmCommand.data[(j * 2) + 2];
                            // save state
                            ymState[port][reg] = value;
                            // create command
                            commands.Add(new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) reg, value}));
                        }
                        break;

                    case XGMFMCommand.FM0_PAN:
                    case XGMFMCommand.FM1_PAN:
                        reg = 0xB4 + ch;
                        value = (byte) ((ymState[port][reg] & 0x3F) | ((fmCommand.data[0] << 4) & 0xC0));
                        // save state
                        ymState[port][reg] = value;
                        // set command
                        commands.Add(new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) reg, value}));
                        break;

                    case XGMFMCommand.FM_FREQ:
                    case XGMFMCommand.FM_FREQ_WAIT:
                        // pre-key off ?
                        if ((fmCommand.data[1] & 0x40) != 0)
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, (byte) (0x00 + (port << 2) + ch)}));

                        // special mode ?
                        reg = fmCommand.isYMFreqSpecialWrite() ? 0xA8 : 0xA0;
                        // set channel from slot
                        if (fmCommand.isYMFreqSpecialWrite())
                            ch = fmCommand.getYMSlot() - 1;
                        lvalue = fmCommand.getYMFreqValue();
                        // save state
                        ymState[port][reg + ch + 4] = (byte) ((lvalue >> 8) & 0x3F);
                        ymState[port][reg + ch + 0] = (byte) (lvalue & 0xFF);
                        // create commands
                        commands.Add(
                                new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) (reg + ch + 4), ymState[port][reg + ch + 4]}));
                        commands.Add(
                                new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) (reg + ch + 0), ymState[port][reg + ch + 0]}));

                        // post-key on ?
                        if ((fmCommand.data[1] & 0x80) != 0)
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, (byte) (0xF0 + (port << 2) + ch)}));
                        break;

                    case XGMFMCommand.FM_FREQ_DELTA:
                    case XGMFMCommand.FM_FREQ_DELTA_WAIT:
                        // special mode ?
                        reg = fmCommand.isYMFreqDeltaSpecialWrite() ? 0xA8 : 0xA0;
                        // set channel from slot
                        if (fmCommand.isYMFreqDeltaSpecialWrite())
                            ch = fmCommand.getYMSlot() - 1;
                        // get state
                        lvalue = (ymState[port][reg + ch + 4] & 0x3F) << 8;
                        lvalue |= ymState[port][reg + ch + 0] & 0xFF;
                        lvalue += fmCommand.getYMFreqDeltaValue();
                        // save state
                        ymState[port][reg + ch + 4] = (byte) ((lvalue >> 8) & 0x3F);
                        ymState[port][reg + ch + 0] = (byte) (lvalue & 0xFF);
                        // create commands
                        commands.Add(
                                new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) (reg + ch + 4), ymState[port][reg + ch + 4]}));
                        commands.Add(
                                new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) (reg + ch + 0), ymState[port][reg + ch + 0]}));
                        break;

                    case XGMFMCommand.FM_TL:
                        // compute reg
                        reg = 0x40 + (fmCommand.getYMSlot() << 2) + ch;
                        // save state
                        ymState[port][reg] = (byte) fmCommand.getYMTLValue();
                        // create commands
                        commands.Add(new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) (reg), ymState[port][reg]}));
                        break;

                    case XGMFMCommand.FM_TL_DELTA:
                    case XGMFMCommand.FM_TL_DELTA_WAIT:
                        // compute reg
                        reg = 0x40 + (fmCommand.getYMSlot() << 2) + ch;
                        // get state
                        lvalue = ymState[port][reg] & 0xFF;
                        lvalue += (byte) fmCommand.getYMTLDelta();
                        // save state
                        ymState[port][reg] = (byte) lvalue;
                        // create commands
                        commands.Add(new VGMCommand(new byte[] {(byte) (VGMCommand.WRITE_YM2612_PORT0 + port), (byte) (reg), ymState[port][reg]}));
                        break;

                    case XGMFMCommand.FM_KEY:
                        // create key command
                        commands.Add(new VGMCommand(
                                new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, (byte) ((((fmCommand.data[0] & 8) != 0) ? 0xF0 : 0x00) + (port << 2) + ch)}));
                        break;

                    case XGMFMCommand.FM_KEY_SEQ:
                        // create key sequence commands
                        if ((fmCommand.data[0] & 8) != 0)
                        {
                            // ON-OFF sequence
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, (byte) (0xF0 + (port << 2) + ch)}));
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, (byte) (0x00 + (port << 2) + ch)}));
                        }
                        else
                        {
                            // OFF-ON sequence
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, (byte) (0x00 + (port << 2) + ch)}));
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, (byte) (0xF0 + (port << 2) + ch)}));
                        }
                        break;

                    case XGMFMCommand.FM_KEY_ADV:
                        // create key command
                        commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x28, fmCommand.data[1]}));
                        break;

                    case XGMFMCommand.FM_DAC_ON:
                        // save state
                        ymState[0][0x2B] = (byte) 0x80;
                        commands.Add(new VGMCommand(new byte[] {0x52, (byte) 0x2B, (byte) 0x80}, 0));
                        break;

                    case XGMFMCommand.FM_DAC_OFF:
                        // save state
                        ymState[0][0x2B] = (byte) 0x00;
                        commands.Add(new VGMCommand(new byte[] {0x52, (byte) 0x2B, (byte) 0x00}, 0));
                        break;

                    case XGMFMCommand.FM_LFO:
                        // save state
                        ymState[0][0x22] = fmCommand.data[1];
                        // create command
                        commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_YM2612_PORT0, 0x22, ymState[0][0x22]}));
                        break;

                    case XGMFMCommand.FM_CH3_SPECIAL_ON:
                        // get $27 value
                        value = (byte) ((ymState[0][0x27] & 0xBF) | 0x40);
                        // save state
                        ymState[0][0x27] = value;
                        // create command
                        commands.Add(new VGMCommand(new byte[] {0x52, (byte) 0x27, value}, 0));
                        break;

                    case XGMFMCommand.FM_CH3_SPECIAL_OFF:
                        // get $27 value
                        value = (byte) ((ymState[0][0x27] & 0xBF) | 0x00);
                        // save state
                        ymState[0][0x27] = value;
                        // create command
                        commands.Add(new VGMCommand(new byte[] {0x52, (byte) 0x27, value}, 0));
                        break;
                }
            }
            else if (command is XGMPSGCommand)
            {
                XGMPSGCommand psgCommand = (XGMPSGCommand) command;

                while (time < psgCommand.time)
                {
                    if (rate == 50)
                    {
                        commands.Add(new VGMCommand(new byte[] {(byte) 0x63}));
                        time += 882;
                    }
                    else
                    {
                        commands.Add(new VGMCommand(new byte[] {(byte) 0x62}));
                        time += 735;
                    }
                }

                ch = psgCommand.getChannel();
                int oldhighFreq;

                switch (psgCommand.getType())
                {
                    case XGMPSGCommand.ENV0:
                    case XGMPSGCommand.ENV1:
                    case XGMPSGCommand.ENV2:
                    case XGMPSGCommand.ENV3:
                        // save state
                        psgState[0][ch] = (short) psgCommand.getEnv();
                        // register value
                        value = (byte) ((0x90 + (ch << 5)) | psgState[0][ch]);
                        // create command
                        commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_SN76489, value}));
                        break;

                    case XGMPSGCommand.ENV0_DELTA:
                    case XGMPSGCommand.ENV1_DELTA:
                    case XGMPSGCommand.ENV2_DELTA:
                    case XGMPSGCommand.ENV3_DELTA:
                        // save state
                        psgState[0][ch] = (short) (psgCommand.getEnvDelta() + (psgState[0][ch] & 0xF));
                        // register value
                        value = (byte) ((0x90 + (ch << 5)) | psgState[0][ch]);
                        // create command
                        commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_SN76489, value}));
                        break;

                    case XGMPSGCommand.FREQ:
                    case XGMPSGCommand.FREQ_WAIT:
                        oldhighFreq = psgState[1][ch] & 0x03F0;
                        lvalue = psgCommand.getFreq();
                        // save state
                        psgState[1][ch] = (short) lvalue;
                        // create commands
                        value = (byte) ((0x80 + (ch << 5)) | (lvalue & 0x0F));
                        commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_SN76489, value}));
                        // high byte changed and not channel 3 (single byte write for channel 3)
                        if ((oldhighFreq != (lvalue & 0x3F0)) && (ch < 3))
                        {
                            value = (byte) (0x00 | ((lvalue >> 4) & 0x3F));
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_SN76489, value}));
                        }
                        break;

                    case XGMPSGCommand.FREQ_LOW:
                        lvalue = (psgState[1][ch] & 0x03F0) | (psgCommand.getFreqLow() & 0xF);
                        // save state
                        psgState[1][ch] = (short) lvalue;
                        // create command
                        value = (byte) ((0x80 + (ch << 5)) | (lvalue & 0x0F));
                        commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_SN76489, value}));
                        break;
                        
                    case XGMPSGCommand.FREQ0_DELTA:
                    case XGMPSGCommand.FREQ1_DELTA:
                    case XGMPSGCommand.FREQ2_DELTA:
                    case XGMPSGCommand.FREQ3_DELTA:
                        oldhighFreq = psgState[1][ch] & 0x03F0;
                        lvalue = (psgState[1][ch] & 0x03FF) + psgCommand.getFreqDelta();
                        // save state
                        psgState[1][ch] = (short) lvalue;
                        // create commands
                        value = (byte) ((0x80 + (ch << 5)) | (lvalue & 0x0F));
                        commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_SN76489, value}));
                        // high byte changed and not channel 3 (single byte write for channel 3)
                        if ((oldhighFreq != (lvalue & 0x3F0)) && (ch < 3))
                        {
                            value = (byte) (0x00 | ((lvalue >> 4) & 0x3F));
                            commands.Add(new VGMCommand(new byte[] {VGMCommand.WRITE_SN76489, value}));
                        }
                        break;                        
                }
            }
        }

        // pack wait and update time and offsets for all command
        packWait();

        // we had a loop command ?
        if (loopOffset != -1)
        {
            // find pointed XGM command
            XGMFMCommand command = xgm.getFMCommandAtOffset(loopOffset);

            // found ? --> insert a VGM loop start command at corresponding position
            if (command != null)
                commands.Add(Command.getCommandIndexAtTime(commands, command.time), new VGMCommand.LoopStartCommand());

            // update time for added loop command (offsets don't change as loop commands are dummy)
            updateTimes();
        }

        // end marker
        commands.Add(new VGMCommand(new byte[] {0x66}));

        // update offsets and time
        updateOffsets();
        updateTimes();
    }

    /**
     * Update time information for all command
     */
    public void updateTimes()
    {
        int time = 0;

        foreach (VGMCommand com in commands)
        {
            com.time = time;
            time += com.getWaitValue();
        }
    }

    /**
     * Update origin offset information for all command
     */
    public void updateOffsets()
    {
        Command.computeOffsets(commands, offsetStart);
    }

    public int getTotalTime()
    {
        int size = commands.Count;
        if (size == 0)
            return 0;

        return commands[size - 1].time;
    }

    public int getTimeFrom(VGMCommand from)
    {
        return getTotalTime() - from.time;
    }

    public void parse()
    {
        int time = 0;
        int loopTimeSt = -1;
        int lastPSGLowWrite = 0;

        // parse all VGM commands
        int off = offsetStart;
        while (off < offsetEnd)
        {
            // check for loop start
            if ((loopTimeSt == -1) && (loopStart != 0) && (off >= loopStart))
            {
                commands.Add(new VGMCommand.LoopStartCommand());
                loopTimeSt = time;
            }

            VGMCommand command = new VGMCommand(data, off);
            time += command.getWaitValue();
            off += command.size;

            // PSG write ?
            if (command.isPSGWrite())
            {
                // get write type (tone / env)
                if (command.isPSGLowByteWrite())
                    lastPSGLowWrite = command.getPSGValue();
                // high byte write for env write ?
                else if ((lastPSGLowWrite & 0x10) == 0x10)
                    // format it as low byte write
                    command.data[1] = (byte) ((lastPSGLowWrite & 0xF0) | (command.data[1] & 0x0F)); 
            }

            // stop here (we don't add the end command)
            if (command.isEnd())
                break;

            // add command
            commands.Add(command);
        }

        // we have a loop ?
        if ((loopTimeSt >= 0) && (loopLenInSample != 0))
        {
            int delta = loopLenInSample - (time - loopTimeSt);

            // missing a bit of time before looping ?
            if (delta > (44100 / 100))
                // insert wait frame command
                commands.Add(new VGMCommand((rate == 60) ? VGMCommand.WAIT_NTSC_FRAME : VGMCommand.WAIT_PAL_FRAME));
        }

        // add 'end command'
        commands.Add(new VGMCommand(VGMCommand.END));

        if (Launcher.verbose)
            Console.WriteLine("Number of command: " + commands.Count);
    }

    public void buildSamples(bool convert)
    {
        // builds data blocks (compatible with all versions)
        foreach (VGMCommand command in commands)
            if (command.isDataBlock())
                addDataBlock(command);

        // clean seek
        cleanSeekCommands();
        // clean useless PCM data
        // cleanPlayPCMCommands();

        // extract samples from seek command
        int ind = 0;
        while (ind < commands.Count)
        {
            if (commands[ind].isSeek())
                ind = extractSampleFromSeek(ind, convert);
            else
                ind++;
        }

        // set bank id and frequency to -1 by default
        int[] sampleIdBanks = new int[0x100];
        int[] sampleIdFrequencies = new int[0x100];

        for (int i = 0; i < 0x100; i++)
        {
            sampleIdBanks[i] = -1;
            sampleIdFrequencies[i] = 0;
        }

        // first pass to extract sample info from stream commands
        ind = 0;
        while (ind < commands.Count)
        {
            VGMCommand command = commands[ind];

            // set bank id for given stream
            if (command.isStreamData())
                sampleIdBanks[command.getStreamId()] = command.getStreamBankId();
            // set frequency for given stream
            else if (command.isStreamFrequency())
                sampleIdFrequencies[command.getStreamId()] = command.getStreamFrenquency();
            // short start command
            else if (command.isStreamStart())
            {
                int bankId = sampleIdBanks[command.getStreamId()];
                SampleBank bank = getDataBank(bankId);

                if (bank != null)
                {
                    int sampleId = command.getStreamBlockId();
                    SampleBank.InternalSample sample = bank.getSampleById(sampleId);

                    // sample found --> adjust frequency
                    if (sample != null)
                    {
                        sample.setRate(sampleIdFrequencies[command.getStreamId()]);
                        // convert to long command as we use single data block
                        if (convert)
                            commands.set(ind, sample.getStartLongCommand(sample.len));
                    }
                    else if (!Launcher.silent)
                        Console.WriteLine(J.Format("Sample id %2X not found !", (int)(sampleId)));
                }
                else if (!Launcher.silent)
                    Console.WriteLine(J.Format("Sample bank %2X not found !", (int)(bankId)));
            }

            // long start command
            if (command.isStreamStartLong())
            {
                int bankId = sampleIdBanks[command.getStreamId()];
                SampleBank bank = getDataBank(bankId);

                if (bank != null)
                {
                    int sampleAddress = command.getStreamSampleAddress();
                    int sampleLen = command.getStreamSampleSize();

                    // add sample
                    bank.addSample(sampleAddress, sampleLen, sampleIdFrequencies[command.getStreamId()]);
                }
            }

            ind++;
        }

        if (convert)
        {
            // remove old seek and play PCM commands
            removeSeekAndPlayPCMCommands();
            // rebuild data blocks
            updateSampleDataBlocks();
        }
    }

    public int extractSampleFromSeek(int index, bool convert)
    {
        int seekIndex = index;
        VGMCommand command = commands[seekIndex];
        // get sample address in data bank
        int sampleAddr = command.getSeekAddress();

        SampleBank bank;
        int ind;
        int len;
        int wait;
        int delta;
        double deltaMean;
        int endPlayWait;

        int startPlayInd;
        int endPlayInd;

        // sample stats
        int sampleData;
        int sampleMinData;
        int sampleMaxData;
        double sampleMeanDelta;

        // use the last bank (FIXME: not really nice to do that)
        if (!sampleBanks.IsEmptyJ())
            bank = sampleBanks[sampleBanks.Count - 1];
        else
            bank = null;

        // then find seek command to extract sample
        len = 0;
        wait = -1;
        delta = 0;
        deltaMean = 0d;
        endPlayWait = 0;

        sampleData = 128;
        sampleMinData = 128;
        sampleMaxData = 128;
        sampleMeanDelta = 0;

        startPlayInd = -1;
        endPlayInd = -1;
        ind = seekIndex + 1;
        while (ind < commands.Count)
        {
            command = commands[ind];

            // sample done !
            if (command.isDataBlock() || command.isEnd())
                break;
            if (command.isSeek())
            {
                int seekAddr = command.getSeekAddress();
                int curAddr = sampleAddr + len;

                // seek on different address --> interrupt current play
                if (((curAddr + SAMPLE_ALLOWED_MARGE) < seekAddr) || ((curAddr - SAMPLE_ALLOWED_MARGE) > seekAddr))
                    break;
                else if (Launcher.verbose)
                    Console.WriteLine("Seek command found with small offset change (" + (curAddr - seekAddr) + ") --> considering continue play");
            }

            // playing ?
            if (wait != -1)
            {
                delta = wait - endPlayWait;

                // delta >= 20 means rate < 2200 Hz --> very unlikely, discard it from mean computation
                if (delta < 20)
                {
                    // compute delta mean for further correction
                    if (deltaMean == 0)
                        deltaMean = delta;
                    else
                        deltaMean = (delta * 0.1) + (deltaMean * 0.9);
                }

                // delta > SAMPLE_END_DELAY samples --> sample ended
                if (delta > SAMPLE_END_DELAY)
                {
                    // found a sample --> add it
                    if ((len > 0) && (endPlayWait > 0) && (startPlayInd != endPlayInd))
                    {
                        // ignore too short sample
                        if ((len < SAMPLE_MIN_SIZE) && Launcher.sampleIgnore)
                        {
                            if (Launcher.verbose)
                                Console.WriteLine(
                                        J.Format("Sample at %6X is too small (%d) --> ignored", (int)(sampleAddr), (int)(len)));
                        }
                        // ignore sample with too small dynamic
                        else if (((sampleMaxData - sampleMinData) < SAMPLE_MIN_DYNAMIC) && Launcher.sampleIgnore)
                        {
                            if (Launcher.verbose)
                                Console.WriteLine(J.Format("Sample at %6X has a too quiet global dynamic (%d) --> ignored", (int)(sampleAddr),
                                        (int)(sampleMaxData - sampleMinData)));
                        }
                        // ignore sample too quiet
                        else if (((sampleMeanDelta / len) < SAMPLE_MIN_MEAN_DELTA) && Launcher.sampleIgnore)
                        {
                            if (Launcher.verbose)
                                Console.WriteLine(J.Format("Sample at %6X is too quiet (mean delta value = %g) --> ignored", (int)(sampleAddr),
                                        (double)(sampleMeanDelta / len)));
                        }
                        else if (bank != null)
                        {
                            int r = (int) J.Round((44100d * len) / endPlayWait);
                            SampleBank.InternalSample sample = bank.addSample(sampleAddr, len, r);

                            if (convert)
                            {
                                // insert stream play command
                                commands.Add(startPlayInd + 0, sample.getSetRateCommand(sample.rate));
                                commands.Add(startPlayInd + 1, sample.getStartLongCommand(len));

                                // always insert sample stop as sample len can change
                                // if ((sample.len + SAMPLE_ALLOWED_MARGE) < len)
                                {
                                    // insert stream stop command
                                    commands.Add(endPlayInd + 0, sample.getStopCommand());
                                }
                            }
                        }
                    }

                    // reset
                    sampleAddr += len;
                    len = 0;
                    wait = -1;
                    delta = 0;
                    deltaMean = 0d;
                    endPlayWait = 0;

                    sampleData = 128;
                    sampleMinData = 128;
                    sampleMaxData = 128;
                    sampleMeanDelta = 0;

                    startPlayInd = -1;
                    endPlayInd = -1;
                }
            }

            // compute sample len
            if (command.isPCM())
            {
                // start play --> init wait
                if (wait == -1)
                {
                    wait = 0;
                    startPlayInd = ind;
                }

                // simple fix by using mean
                if (Launcher.sampleRateFix)
                {
                    // need a minimal length before applying correction
                    // if ((len > 100) && (wait > 200))
                    // {
                    // int mean = wait / len;
                    //
                    // // correct abnormal delta
                    // if (delta < (mean - 2))
                    // wait += (mean - delta);
                    // else if (delta > (mean + 2))
                    // wait -= (delta - mean);
                    // }

                    // can correct ?
                    if (deltaMean != 0)
                    {
                        int mean = (int) J.Round(deltaMean);
                        if (delta < (mean - 2))
                            wait += mean - delta;
                        else if (delta > (mean + 2))
                            wait -= delta - mean;
                    }
                }

                // keep trace of last play wait value
                endPlayWait = wait;
                endPlayInd = ind;

                // get current sample value
                if (bank != null)
                {
                    // inside the bank
                    if (sampleAddr + len < bank.getLength())
                    {
                        int d = Util.getInt8(bank.data, sampleAddr + len);

                        sampleMeanDelta += Math.Abs(d - sampleData);
                        if (sampleMinData > d)
                            sampleMinData = d;
                        if (sampleMaxData < d)
                            sampleMaxData = d;
                        sampleData = d;
                    }
                }

                wait += command.getWaitValue();
                len++;
            }
            // playing ?
            else if (wait != -1)
                wait += command.getWaitValue();

            ind++;
        }

        // found a sample --> add it
        if ((len > 0) && (endPlayWait > 0) && (startPlayInd != endPlayInd))
        {
            // ignore too short sample
            if ((len < SAMPLE_MIN_SIZE) && Launcher.sampleIgnore)
            {
                if (Launcher.verbose)
                    Console.WriteLine(J.Format("Sample at %6X is too small (%d) --> ignored", (int)(sampleAddr), (int)(len)));
            }
            // ignore sample with too small dynamic
            else if (((sampleMaxData - sampleMinData) < SAMPLE_MIN_DYNAMIC) && Launcher.sampleIgnore)
            {
                if (Launcher.verbose)
                    Console.WriteLine(J.Format("Sample at %6X has a too quiet global dynamic (%d) --> ignored", (int)(sampleAddr),
                            (int)(sampleMaxData - sampleMinData)));
            }
            // ignore sample too quiet
            else if (((sampleMeanDelta / len) < SAMPLE_MIN_MEAN_DELTA) && Launcher.sampleIgnore)
            {
                if (Launcher.verbose)
                    Console.WriteLine(J.Format("Sample at %6X is too quiet (mean delta value = %g) --> ignored", (int)(sampleAddr),
                            (double)(sampleMeanDelta / len)));
            }
            else if (bank != null)
            {
                int r = (int) J.Round((44100d * len) / endPlayWait);
                SampleBank.InternalSample sample = bank.addSample(sampleAddr, len, r);

                if (convert)
                {
                    // insert stream play command
                    commands.Add(startPlayInd + 0, sample.getSetRateCommand(sample.rate));
                    commands.Add(startPlayInd + 1, sample.getStartLongCommand(len));

                    // always insert sample stop as sample len can change
                    // if ((sample.len + SAMPLE_ALLOWED_MARGE) < len)
                    {
                        // insert stream stop command
                        commands.Add(endPlayInd + 0, sample.getStopCommand());
                    }
                }
            }
        }

        return ind;
    }

    public SampleBank getDataBank(int id)
    {
        foreach (SampleBank bank in sampleBanks)
            if (bank.id == id)
                return bank;

        return null;
    }

    public SampleBank addDataBlock(VGMCommand command)
    {
        SampleBank result;

        result = getDataBank(command.getDataBankId());
        // different id --> new bank
        if (result == null)
        {
            // more than 1 bank ?
            if (!Launcher.silent && !sampleBanks.IsEmptyJ())
                Console.WriteLine("Warning: VGM file contains more than 1 data block bank (may not work correctly)");

            result = new SampleBank(command);
            sampleBanks.Add(result);
        }
        // same id --> concat block
        else
            result.addBlock(command);

        return result;
    }

    public void cleanSeekCommands()
    {
        HashSet<VGMCommand> removed = new(commands.Count);
        bool samplePlayed = false;

        for (int ind = commands.Count - 1; ind >= 0; ind--)
        {
            VGMCommand command = commands[ind];

            // seek command ?
            if (command.isSeek())
            {
                // no sample played after this seek command --> remove it
                if (!samplePlayed)
                {
                    if (Launcher.verbose)
                        Console.WriteLine(J.Format("Useless seek command found at %6X", (int)(command.getOriginOffset())));
                    removed.Add(commands[ind]);
                }

                samplePlayed = false;
            }
            else if (command.isPCM())
                samplePlayed = true;
        }

        if (!removed.IsEmptyJ())
        {
            // rebuild the command list without useless seek
            List<VGMCommand> newComms = new(commands.Count);

            foreach (VGMCommand com in commands)
                if (!removed.Contains(com))
                    newComms.Add(com);

            commands = newComms;
        }

        // update time and offsets for all command
        updateTimes();
        updateOffsets();
    }

    public void removeDummyStreamStopCommands()
    {
        for (int ind = commands.Count - 2; ind >= 0; ind--)
        {
            // remove stream stop command followed by stream frequency (that means we start another sample immediately)
            if (commands[ind].isStreamStop())
                if (commands[ind + 1].isStreamFrequency())
                    commands.remove(ind);
        }
    }

    public void removeSeekAndPlayPCMCommands()
    {
        List<VGMCommand> newComms = new(commands.Count);

        for (int ind = 0; ind < commands.Count; ind++)
        {
            VGMCommand command = commands[ind];

            // remove Seek command
            if (command.isSeek())
                continue;
            // replace PCM command by simple wait command
            else if (command.isPCM())
            {
                int wait = command.getWaitValue();

                // remove or just replace by wait command
                if (wait == 0)
                    continue;

                newComms.Add(new VGMCommand(0x70 + (wait - 1)));
            }
            else
                newComms.Add(command);
        }

        commands = newComms;

        if (!Launcher.silent)
            Console.WriteLine("Number of command after PCM commands remove: " + commands.Count);
    }

    public void cleanKeyCommands(List<VGMCommand> frameCommands)
    {
        HashSet<VGMCommand> toRemove = new();

        bool[] hasKeyOn = new bool[6];
        bool[] hasKeyOff = new bool[6];

        // start from end of frame
        for (int c = frameCommands.Count - 1; c >= 0; c--)
        {
            VGMCommand com = frameCommands[c];

            if (com.isYM2612KeyOnWrite())
            {
                int ch = com.getYM2612Channel();

                // can't have several key-on in a single frame
                if ((ch == -1) || hasKeyOn[ch])
                    toRemove.Add(com);
                else
                    hasKeyOn[ch] = true;
            }
            else if (com.isYM2612KeyOffWrite())
            {
                int ch = com.getYM2612Channel();

                // can't have more than 2 key-off in a single frame
                if ((ch == -1) || hasKeyOff[ch])
                    toRemove.Add(com);
                else
                {
                    // allow an extra key-off after a key-on
                    if (hasKeyOn[ch])
                        hasKeyOff[ch] = true;
                }
            }
        }

        List<VGMCommand> temp = new();
        foreach (VGMCommand com in frameCommands)
            if (!toRemove.Contains(com))
                temp.Add(com);

        bool?[] keyState = new bool?[6];
        // then in normal frame order
        foreach (VGMCommand com in temp)
        {
            if (com.isYM2612KeyOnWrite())
            {
                int ch = com.getYM2612Channel();

                // already on
                if (keyState[ch] == true)
                    toRemove.Add(com);

                keyState[ch] = true;
            }
            else if (com.isYM2612KeyOffWrite())
            {
                int ch = com.getYM2612Channel();

                // already off
                if (keyState[ch] == false)
                    toRemove.Add(com);

                keyState[ch] = false;
            }
        }

        // finally rebuild frameCommands
        frameCommands.Clear();
        foreach (VGMCommand com in temp)
            if (!toRemove.Contains(com))
                frameCommands.Add(com);
    }

    public void cleanCommands()
    {
        List<VGMCommand> frameCommands = new();

        List<VGMCommand> newCommands = new();
        List<VGMCommand> optimizedCommands = new();
        List<VGMCommand> keyOnOffCommands = new();
        List<VGMCommand> ymCommands = new();
        List<VGMCommand> lastCommands = new();

        YM2612State ymLoopState;
        PSGState psgLoopState;
        YM2612State ymOldState;
        YM2612State ymState;
        PSGState psgOldState;
        PSGState psgState;

        ymLoopState = null;
        psgLoopState = null;
        ymOldState = new YM2612State();
        ymState = new YM2612State();
        psgOldState = new PSGState();
        psgState = new PSGState();

        VGMCommand command;
        int startInd;
        int endInd;
        
        startInd = 0;
        while (true)
        {
            endInd = startInd;
            frameCommands.Clear();

            // build frame commands
            do
            {
                command = commands[endInd];
                frameCommands.Add(command);
                endInd++;
            }
            while (endInd < commands.Count && !command.isWait() && !command.isEnd());

            // clean duplicated key com
            cleanKeyCommands(frameCommands);

            psgState = new PSGState(psgOldState);
            ymState = new YM2612State(ymOldState);
            
            // first frame ? --> reset special FM feature state (otherwise they may be not properly reseted on start play)
            if (startInd == 0)
            {
                // LFO
                ymState.set(0, 0x22, 0);
                // FM2 SPE mode / CSM
                ymState.set(0, 0x27, 0);
                // default panning
                ymState.set(0, 0xB4, 0xC0);
                ymState.set(0, 0xB5, 0xC0);
                ymState.set(0, 0xB6, 0xC0);
                ymState.set(1, 0xB4, 0xC0);
                ymState.set(1, 0xB5, 0xC0);
                ymState.set(1, 0xB6, 0xC0);
            }

            // clear frame sets
            optimizedCommands.Clear();
            keyOnOffCommands.Clear();
            ymCommands.Clear();
            lastCommands.Clear();

            bool hasKeyCom = false;
            for (int fi = 0; fi < frameCommands.Count; fi++)
            {
                command = frameCommands[fi];
                
                // keep data block / stream and loop commands at first
                if (command.isDataBlock() || command.isStream() || command.isLoopStart())
                {
                    optimizedCommands.Add(command);

                    // loop start ?
                    if (command.isLoopStart())
                    {
                        // save loop state
                        ymLoopState = new YM2612State(ymOldState);
                        psgLoopState = new PSGState(psgOldState);
                    }
                }
                else if (command.isPSGWrite())
                    psgState.write(command.getPSGValue());
                else if (command.isYM2612Write())
                {
                    // key write ?
                    if (command.isYM2612KeyWrite())
                    {
                        // key state really changed ?
                        if (ymState.set(command.getYM2612Port(), command.getYM2612Register(), command.getYM2612Value()))
                        {
                            // store it as getDelta won't return it
                            keyOnOffCommands.Add(command);
                            hasKeyCom = true;
                        }
                    }
                    // other write ?
                    else
                    {
                        // check first if we need to flush commands (for accurate order of key events / register writes)
                        if (hasKeyCom)
                        {
                            // add frame commands for delta YM
                            ymCommands.AddRange(ymOldState.getDelta(ymState, true));
                            // add frame commands for key on/off
                            ymCommands.AddRange(keyOnOffCommands);

                            keyOnOffCommands.Clear();

                            // update old state
                            ymOldState = new YM2612State(ymState);

                            hasKeyCom = false;
                        }

                        // write to YM state and check if state changed
                        ymState.set(command.getYM2612Port(), command.getYM2612Register(), command.getYM2612Value());
                    }
                }
                // add frame commands at last
                else if (command.isWait() || command.isSeek())
                    lastCommands.Add(command);
                else
                {
                    if (Launcher.verbose)
                        Console.WriteLine("Command ignored: " + StringUtil.toHexaString(command.getCommand(), 2));
                }
            }

            bool hasStreamStart = false;
            bool hasStreamRate = false;
            // check we have single stream per frame (start from end)
            int ind = optimizedCommands.Count - 1;
            while (ind >= 0)
            {
                command = optimizedCommands[ind];

                if (command.isStreamStartLong())
                {
                    if (hasStreamStart)
                    {
                        if (!Launcher.silent)
                        {
                            Console.WriteLine("Warning: more than 1 PCM command in a single frame !");
                            Console.WriteLine("Command stream start " + command + " removed at " + J.D2S(command.time / 44100d));
                        }

                        optimizedCommands.remove(ind);
                    }

                    hasStreamStart = true;
                }
                else if (command.isStreamFrequency())
                {
                    if (hasStreamRate)
                    {
                        if (Launcher.verbose)
                            Console.WriteLine("Command stream rate " + command + " removed at " + J.D2S(command.time / 44100d));

                        optimizedCommands.remove(ind);
                    }

                    hasStreamRate = true;
                }

                ind--;
            }
            
            // end of track ?
            if (endInd >= commands.Count || command.isEnd())
            {
                // loop point ? --> use YM / PSG loop point state for proper state restoration 
                if (ymLoopState != null) ymState = ymLoopState;
                if (psgLoopState != null) psgState = psgLoopState;
            }

            // send first merged YM commands (with intermediate key on/off)
            optimizedCommands.AddRange(ymCommands);
            // add frame commands for delta YM
            optimizedCommands.AddRange(ymOldState.getDelta(ymState, true));
            // add frame commands for key on/off
            optimizedCommands.AddRange(keyOnOffCommands);
            // add frame commands for delta PSG
            optimizedCommands.AddRange(psgOldState.getDelta(psgState));
            // add frame commands
            optimizedCommands.AddRange(lastCommands);

            // add frame optimized set to new commands
            newCommands.AddRange(optimizedCommands);

            // end of the track --> stop here
            if (endInd >= commands.Count || command.isEnd()) break;

            // update states
            ymOldState = new YM2612State(ymState);
            psgOldState = new PSGState(psgState);

            // next frame
            startInd = endInd;
        }

        // end command
        newCommands.Add(new VGMCommand(VGMCommand.END));

        commands = newCommands;
        // update time and offsets for all command
        updateTimes();
        updateOffsets();

        if (!Launcher.silent)
        {
            Console.WriteLine("Music data size: " + getMusicDataSize());
            Console.WriteLine(J.Format("Len (samples): %d", (int)(getTotalTime())));
            Console.WriteLine("Number of command after commands clean: " + commands.Count);
        }
    }

    public void cleanSamples()
    {
        if (Launcher.verbose)
            Console.WriteLine("Clean samples");

        // detect unused samples
        for (int b = sampleBanks.Count - 1; b >= 0; b--)
        {
            SampleBank bank = sampleBanks[b];
            int bankId = bank.id;

            for (int s = bank.samples.Count - 1; s >= 0; s--)
            {
                SampleBank.InternalSample sample = bank.samples[s];
                int sampleId = sample.id;
                int minLen = Math.Max(0, sample.len - 50);
                int maxLen = sample.len + 50;
                bool used = false;
                int currentBankId = -1;

                for (int c = 0; c < commands.Count - 1; c++)
                {
                    VGMCommand command = commands[c];

                    if (command.isStreamData())
                        currentBankId = command.getStreamBankId();

                    if (bankId == currentBankId)
                    {
                        if (command.isStreamStart())
                        {
                            if (sampleId == command.getStreamBlockId())
                            {
                                used = true;
                                break;
                            }
                        }
                        else if (command.isStreamStartLong())
                        {
                            int sampleLen = command.getStreamSampleSize();

                            if (sample.matchAddress(command.getStreamSampleAddress()) && (sampleLen >= minLen) && (sampleLen <= maxLen))
                            {
                                used = true;
                                break;
                            }
                        }
                    }
                }

                // sample not used --> remove it
                if (!used)
                {
                    if (Launcher.verbose)
                        Console.WriteLine(J.Format("Sample at offset %6X (len = %d) is not used --> removed", (int)(sample.addr),
                                (int)(sample.len)));
                    bank.samples.remove(s);
                }
            }
        }

        // save old sample addresses (map<comm_offset, sample_address>)
        JMap<int, int> oldSampleAddresses = new JMap<int, int>();
        SampleBank currentBank = null;

        for (int c = 0; c < commands.Count - 1; c++)
        {
            VGMCommand command = commands[c];

            if (command.isStreamData())
                currentBank = getDataBank(command.getStreamBankId());

            if (command.isStreamStartLong() && (currentBank != null))
            {
                SampleBank.InternalSample sample = currentBank.getSampleByAddress(command.getStreamSampleAddress());

                // store command offset / old sample address couple
                if (sample != null)
                    oldSampleAddresses.put((int)(command.getOriginOffset()), (int)(sample.addr));
                else
                    Console.Error.WriteLine("Warning: cleanSamples - Cannot find matching sample address 0x" + StringUtil.toHexaString(command.getStreamSampleAddress()));
            }
        }

        // map containing <id_bank, <sample_old_addr, sample_new_addr>>
        JMap<int, JMap<int, int>> bankSampleAddrChange = new();

        // optimize sample data banks (remove unused data)
        foreach (SampleBank bank in sampleBanks)
            bankSampleAddrChange.put((int)(bank.id), bank.optimize());

        // update samples address
        JMap<int, int> sampleAddressChanges = null;
        for (int c = 0; c < commands.Count - 1; c++)
        {
            VGMCommand command = commands[c];

            // get sample address changes for the current bank
            if (command.isStreamData())
                sampleAddressChanges = bankSampleAddrChange.get((int)(command.getStreamBankId()));

            // long stream start command ? --> need to update it
            if (command.isStreamStartLong())
            {
                // no bank set ?
                if (sampleAddressChanges == null)
                {
                    Console.Error.WriteLine("Warning: cleanSamples - Cannot update sample address in StreamStartLong command at offset 0x"
                            + StringUtil.toHexaString(command.getOriginOffset()));
                    continue;
                }

                int oldAddr = oldSampleAddresses.get((int)(command.getOriginOffset()));

                // should always be the case
                if (oldAddr != null)
                {
                    // get new sample address
                    int newAddr = sampleAddressChanges[oldAddr];

                    if (newAddr != null)
                        command.setStreamSampleAddress(newAddr);
                    else
                        Console.Error.WriteLine("Warning: cleanSamples - Cannot update sample address in StreamStartLong command at offset 0x"
                                + StringUtil.toHexaString(command.getOriginOffset()));
                }
                else
                    Console.Error.WriteLine("Warning: cleanSamples - Cannot update sample address in StreamStartLong command at offset 0x"
                            + StringUtil.toHexaString(command.getOriginOffset()));
            }
        }

        // update sample banks declaration
        updateSampleDataBlocks();

        if (Launcher.verbose)
            Console.WriteLine("Sample num = " + getSampleNumber() + " - data size: " + getSampleDataSize());
    }

    public void updateSampleDataBlocks()
    {
        List<VGMCommand> newComms = new(commands.Count);

        foreach (SampleBank bank in sampleBanks)
        {
            // add data block / declaration for each sample
            newComms.Add(bank.getDataBlockCommand());
            newComms.AddRange(bank.getDeclarationCommands());
        }

        // remove previous data blocks
        for (int ind = 0; ind < commands.Count; ind++)
        {
            VGMCommand command = commands[ind];

            // don't add them
            if (command.isDataBlock() || command.isStreamControl() || command.isStreamData())
                continue;

            newComms.Add(commands[ind]);
        }

        commands = newComms;
    }

    public void fixKeyCommands()
    {
        List<VGMCommand> delayedCommands = new();
        // maximum delta time allowed for key command (1/4 of frame)
        int maxDelta = (44100 / rate) / 4;
        int[] keyOffTime = new int[6];
        int[] keyOnTime = new int[6];
        int frame, i;

        delayedCommands.Clear();
        for (i = 0; i < 6; i++)
        {
            keyOffTime[i] = -1;
            keyOnTime[i] = -1;
        }

        // this method should be called after waits has been converted to frame wait
        frame = 0;
        int ind = 0;
        while (ind < commands.Count)
        {
            VGMCommand command = commands[ind];

            // new frame
            if (command.isWait())
            {
                // some delayed commands ?
                if (!delayedCommands.IsEmptyJ())
                {
                    // insert them right after current command
                    commands.AddRange(ind + 1, delayedCommands);
                    ind += delayedCommands.Count;
                    delayedCommands.Clear();
                }

                // reset key traces
                for (i = 0; i < 6; i++)
                {
                    keyOffTime[i] = -1;
                    keyOnTime[i] = -1;
                }

                frame++;
            }
            else
            {
                if (command.isYM2612KeyWrite())
                {
                    int ch = command.getYM2612Channel();

                    if (ch != -1)
                    {
                        // key off command ?
                        if (command.isYM2612KeyOffWrite())
                        {
                            keyOffTime[ch] = command.time;

                            // previous key on in same frame ?
                            if (keyOnTime[ch] != -1)
                            {
                                // delta time with previous key on is > max delta --> delayed key Off command
                                if ((command.time != -1) && ((command.time - keyOnTime[ch]) > maxDelta))
                                {
                                    if (Launcher.delayKeyOff)
                                    {
                                        if (!Launcher.silent)
                                        {
                                            Console.WriteLine("Warning: CH" + ch + " delayed key OFF command at frame " + frame);
                                            Console.WriteLine("You can try to use the -dd switch if you experience missing or incorrect FM instrument sound.");
                                        }

                                        // remove current command from list
                                        commands.remove(command);

                                        // add to delayed only if we don't already have delayed key off for this channel
                                        if (VGMCommand.getKeyOffCommand(delayedCommands, ch) == null)
                                            delayedCommands.Add(command);
                                    }
                                    else if (!Launcher.silent)
                                    {
                                        Console.WriteLine("Warning: CH" + ch + " key ON/OFF events occured at frame " + frame
                                                + " and delayed key OFF has been disabled.");
                                    }
                                }
                            }
                        }
                        // key on command ?
                        else
                        {
                            keyOnTime[ch] = command.time;

                            // not a good idea to delay key on

                            // // previous key off in same frame ?
                            // if (keyOffTime[ch] != -1)
                            // {
                            // // delta time with previous key off is > max delta --> delayed key on command
                            // if ((command->time != -1) && ((command->time - keyOffTime[ch]) > maxDelta))
                            // {
                            // if (!silent)
                            // Console.WriteLine("Warning: delayed key on ch%d command at frame %d", ch, frame);
                            //
                            // // remove command from list
                            // removeFromLList(commands);
                            //
                            // // add to delayed only if we don't already have delayed key on for this channel
                            // if (VGMCommand_getKeyOnCommand(getHeadLList(delayedCommands), ch) == NULL)
                            // delayedCommands = insertAfterLList(delayedCommands, command);
                            // }
                            // }
                        }
                    }
                }
            }

            ind++;
        }
    }

    public SampleBank.InternalSample getSample(int sampleAddress)
    {
        foreach (SampleBank bank in sampleBanks)
        {
            SampleBank.InternalSample sample = bank.getSampleByAddress(sampleAddress);

            if (sample != null)
                return sample;
        }

        return null;
    }

    public void convertWaits()
    {
        List<VGMCommand> newCommands = new();
        // number of sample per frame
        int samplePerFrame = 44100 / rate;
        // -15%
        int samplePerFramePercent = ((samplePerFrame * 15) / 100);
        int samplePerFrameLimit = samplePerFrame - samplePerFramePercent;
        int comWait = (rate == 60) ? VGMCommand.WAIT_NTSC_FRAME : VGMCommand.WAIT_PAL_FRAME;

        // force update times now
        updateTimes();
        if (Launcher.verbose)
            Console.Write(J.Format("Original len (samples): %d", (int)(getTotalTime())));

        int sampleCnt = 0;
        int lastWait = 0;
        foreach (VGMCommand command in commands)
        {
            // add no wait command
            if (!command.isWait())
                newCommands.Add(command);
            else
            {
                lastWait = command.getWaitValue();
                sampleCnt += lastWait;
            }

            while (sampleCnt > ((lastWait > samplePerFramePercent) ? samplePerFrameLimit : samplePerFrame))
            {
                newCommands.Add(new VGMCommand(comWait));
                sampleCnt -= samplePerFrame;
            }
        }

        // set new commands
        commands = newCommands;
        // update time and offsets for all command
        updateTimes();
        updateOffsets();

        if (Launcher.verbose)
        {
            Console.WriteLine(J.Format(" - new len after wait conversion: %d", (int)(getTotalTime())));
            Console.WriteLine("Number of command: " + commands.Count);
        }
    }

    public void packWait()
    {
        List<VGMCommand> newCommands = new(commands.Count);

        int c = 0;
        while (c < commands.Count)
        {
            // get next wait command
            while (!commands[c].isWait())
            {
                // rebuild new command set
                newCommands.Add(commands[c]);

                // done ? --> stop here
                if (++c >= commands.Count)
                {
                    commands = newCommands;
                    updateTimes();
                    updateOffsets();
                    return;
                }
            }

            int wait = 0;
            // get next no wait command
            while (commands[c].isWait())
            {
                // sum wait
                wait += commands[c].getWaitValue();
                // done ? --> stop here
                if (++c >= commands.Count)
                    break;
            }

            // add new wait commands
            newCommands.AddRange(VGMCommand.createWaitCommands(wait));
        }

        // set new commands
        commands = newCommands;

        // update time and offsets for all command
        updateTimes();
        updateOffsets();
    }

    public void shiftSamples(int sft)
    {
        if (sft == 0)
            return;

        List<VGMCommand>[] sampleCommands;
        sampleCommands = new List<VGMCommand>[sft];

        for (int i = 0; i < sampleCommands.Length; i++)
            sampleCommands[i] = new();

        int frameRead = 0;
        int frameWrite = 1;
        int index = commands.Count - 1;
        while (index >= 0)
        {
            VGMCommand command = commands[index];

            if (command.isStream())
            {
                sampleCommands[frameRead].Add(command);
                commands.remove(index);
            }
            else if (command.isWait() || command.isEnd())
            {
                frameRead = (frameRead + 1) % sft;
                frameWrite = (frameWrite + 1) % sft;

                // add sample command to this frame
                while (!sampleCommands[frameWrite].IsEmptyJ())
                    commands.Add(index, sampleCommands[frameWrite].remove(sampleCommands[frameWrite].Count - 1));
            }

            index--;
        }

        // add last remaining samples
        for (int i = 0; i < sampleCommands.Length; i++)
            while (!sampleCommands[i].IsEmptyJ())
                commands.Add(0, sampleCommands[i].remove(sampleCommands[i].Count - 1));

        // update time and offsets for all command
        updateTimes();
        updateOffsets();
    }

    public int getSampleDataSize()
    {
        int result = 0;

        foreach (SampleBank bank in sampleBanks)
            result += bank.getLength();

        return result;
    }

    public int getSampleTotalLen()
    {
        int result = 0;

        foreach (SampleBank bank in sampleBanks)
            foreach (SampleBank.InternalSample sample in bank.samples)
                result += sample.len;

        return result;
    }

    public int getSampleNumber()
    {
        int result = 0;

        foreach (SampleBank sampleBank in sampleBanks)
            result += sampleBank.samples.Count;

        return result;
    }

    public int getMusicDataSize()
    {
        int result = 0;

        foreach (VGMCommand command in commands)
            if (!command.isDataBlock())
                result += command.size;

        return result;
    }

    public byte[] asByteArray()
    {
        int gd3Offset = 0;
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        // 00: VGM
        result.write(Util.getBytes("Vgm "));
        // 04: len (reserve 4 bytes)
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 08: version 1.60
        result.write(0x60);
        result.write(0x01);
        result.write(0x00);
        result.write(0x00);
        // 0C: SN76489 clock
        result.write(0x99);
        result.write(0x9E);
        result.write(0x36);
        result.write(0x00);
        // 10: YM2413 clock
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 14: GD3 offset
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 18: total number of sample (44100 samples per second)
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 1C: loop offset
        result.write(0);
        result.write(0);
        result.write(0);
        result.write(0);
        // 20: loop number of samples (44100 samples per second)
        result.write(0);
        result.write(0);
        result.write(0);
        result.write(0);
        // 24: rate (50 or 60 Hz)
        result.write(0x3c);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 28: SN76489 flags
        result.write(0x09);
        result.write(0x00);
        result.write(0x10);
        result.write(0x00);
        // 2C: YM2612 clock
        result.write(0xB5);
        result.write(0x0A);
        result.write(0x75);
        result.write(0x00);
        // 30: YM2151 clock
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 34: VGM data offset
        result.write(0x4C);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 38: Sega PCM clock
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 3C: Sega PCM interface
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        result.write(0x00);
        // 40-80
        for (int i = 0x40; i < 0x80; i++)
            result.write(0x00);

        VGMCommand loopCommand = null;
        int loopOffset = 0;

        // write command (ignore loop marker)
        foreach (VGMCommand command in commands)
        {
            // store start loop position
            if (command.isLoopStart())
            {
                loopCommand = command;
                loopOffset = result.size() - 0x1C;
            }
            else
                // just write command
                result.write(command.asByteArray());
        }

        // write GD3 tags if present
        if (gd3 != null)
        {
            // get GD3 offset
            gd3Offset = result.size();
            result.write(gd3.asByteArray());
        }

        byte[] array = result.toByteArray();

        if (loopCommand != null)
        {
            // set loop offset
            Util.setInt32(array, 0x1C, loopOffset);
            // and loop duration
            Util.setInt32(array, 0x20, getTimeFrom(loopCommand));
        }
        // set GD3 offset
        if (gd3 != null)
            Util.setInt32(array, 0x14, gd3Offset - 0x14);
        // set file size
        Util.setInt32(array, 0x04, array.Length - 4);
        // set len in sample
        Util.setInt32(array, 0x18, getTotalTime() - 1);

        return array;
    }
}