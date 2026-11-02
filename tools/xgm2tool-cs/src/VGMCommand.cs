using System.Text;
namespace Sgdk.Xgm2Tool;

/**
 * 
 */



/**
 * VGM command descriptor class
 * 
 * @author Stephane Dallongeville
 */
public class VGMCommand : Command
{
    public const int DATA_BLOCK = 0x67;
    public const int END = 0x66;
    public const int SEEK = 0xE0;

    public const int WRITE_SN76489 = 0x50;
    public const int WRITE_YM2612_PORT0 = 0x52;
    public const int WRITE_YM2612_PORT1 = 0x53;

    public const int WAIT_NTSC_FRAME = 0x62;
    public const int WAIT_PAL_FRAME = 0x63;

    public const int STREAM_CONTROL = 0x90;
    public const int STREAM_DATA = 0x91;
    public const int STREAM_FREQUENCY = 0x92;
    public const int STREAM_START_LONG = 0x93;
    public const int STREAM_START = 0x95;
    public const int STREAM_STOP = 0x94;

    public const int LOOP_START = 0x30;
    public const int LOOP_END = 0x31;

    public static int computeSize(byte[] data, int offset)
    {
        if (data.Length == 0)
            return 0;

        int command = Util.getInt8(data, offset);

        switch (command)
        {
            case 0x4F:
            case WRITE_SN76489:
                return 2;

            case 0x51:
            case WRITE_YM2612_PORT0:
            case WRITE_YM2612_PORT1:
            case 0x54:
            case 0x55:
            case 0x56:
            case 0x57:
            case 0x58:
            case 0x59:
            case 0x5A:
            case 0x5B:
            case 0x5C:
            case 0x5D:
            case 0x5E:
            case 0x5F:
                return 3;

            case 0x61:
                return 3;

            case WAIT_NTSC_FRAME:
            case WAIT_PAL_FRAME:
                return 1;

            case 0x66:
                return 1;

            case DATA_BLOCK:
                // data block start
                return 7 + Util.getInt32(data, offset + 0x03);

            case 0x68:
                // write data block start
                return 12 + Util.getInt24(data, offset + 0x09);

            case STREAM_CONTROL:
                return 5;
            case STREAM_DATA:
                return 5;
            case STREAM_FREQUENCY:
                return 6;
            case STREAM_START_LONG:
                return 11;
            case STREAM_STOP:
                return 2;
            case STREAM_START:
                return 5;
        }

        switch (command >> 4)
        {
            default:
                return 1;
            case 0x3:
                return 2;
            case 0x4:
                return 3;
            case 0x7:
                return 1;
            case 0x8:
                return 1;
            case 0xA:
                return 3;
            case 0xB:
                return 3;
            case 0xC:
                return 4;
            case 0xD:
                return 4;
            case 0xE:
                return 5;
            case 0xF:
                return 5;
        }
    }

    public static int lastPSGChannel = 0;
    
    public VGMCommand(byte[] data, int offset) : base(data)
    {

        // compute size
        size = computeSize(data, offset);
        // then fix data array to not have any offset
        this.data = J.CopyOfRange(data, offset, offset + size);
    }

    public VGMCommand(byte[] data) : this(data, 0)
    {

        // compute size
        size = data.Length;
    }

    public VGMCommand(int command) : this(new byte[] {(byte) command})
    {
    }

    public int getCommand()
    {
        if (this is LoopStartCommand)
            return LOOP_START;

        return Util.getInt8(data, 0);
    }

    public bool isDataBlock()
    {
        return getCommand() == DATA_BLOCK;
    }

    public int getDataBankId()
    {
        return Util.getInt8(data, 2);
    }

    public int getDataBlockLen()
    {
        return Util.getInt32(data, 3);
    }

    public bool isSeek()
    {
        return getCommand() == SEEK;
    }

    public int getSeekAddress()
    {
        if (isSeek())
            return Util.getInt32(data, 0x01);

        return -1;
    }

    public bool isEnd()
    {
        return getCommand() == END;
    }

    public bool isLoopStart()
    {
        return this is LoopStartCommand;
    }

    public bool isPCM()
    {
        return (getCommand() & 0xF0) == 0x80;
    }

    public bool isWait()
    {
        if (isShortWait())
            return true;
        return ((getCommand() >= 0x61) && (getCommand() <= 0x63));
    }

    public bool isShortWait()
    {
        return (getCommand() & 0xF0) == 0x70;
    }

    public int getWaitValue()
    {
        if (isShortWait())
            return (getCommand() & 0x0F) + 1;
        if (isPCM())
            return getCommand() & 0x0F;

        switch (getCommand())
        {
            case 0x61:
                return Util.getInt16(data, 1);
            case WAIT_NTSC_FRAME:
                return 0x2DF;
            case WAIT_PAL_FRAME:
                return 0x372;
        }

        return 0;
    }

    public int computeSize()
    {
        return computeSize(data, 0);
    }

    public bool isPSGWrite()
    {
        return (getCommand() == WRITE_SN76489);
    }

    public bool isPSGLowByteWrite()
    {
        return isPSGWrite() && ((getPSGValue() & 0x80) == 0x80);
    }

    public bool isPSGHighByteWrite()
    {
        return isPSGWrite() && ((getPSGValue() & 0x80) == 0x00);
    }

    public bool isPSGEnvWrite()
    {
        return isPSGLowByteWrite() && ((getPSGValue() & 0x10) == 0x10);
    }

    public bool isPSGToneWrite()
    {
        // ok to assume that as we force PSG env write to use low byte write format
        return isPSGWrite() && !isPSGEnvWrite();
    }

    public bool isPSGToneLowWrite()
    {
        return isPSGToneWrite() && isPSGLowByteWrite();
    }

    public bool isPSGToneHighWrite()
    {
        return isPSGToneWrite() && isPSGHighByteWrite();
    }

    public int getPSGValue()
    {
        if (isPSGWrite())
            return Util.getInt8(data, 1);

        return -1;
    }

    public int getPSGChannel()
    {
        if (isPSGWrite())
        {
            if (isPSGLowByteWrite())
                lastPSGChannel = (getPSGValue() >> 5) & 3;
            
            return lastPSGChannel;
        }

        return -1;
    }

    public int getPSGFrequence()
    {
        if (isPSGToneHighWrite())
            return ((getPSGValue() & 0x3F) << 4);
        if (isPSGToneLowWrite())
            return (getPSGValue() & 0xF);

        return -1;
    }

    public int getPSGEnv()
    {
        if (isPSGEnvWrite())
            return (getPSGValue() & 0xF);

        return -1;
    }

    public bool isYM2612Port0Write()
    {
        return (getCommand() == WRITE_YM2612_PORT0);
    }

    public bool isYM2612Port1Write()
    {
        return (getCommand() == WRITE_YM2612_PORT1);
    }

    public bool isYM2612Write()
    {
        return isYM2612Port0Write() || isYM2612Port1Write();
    }

    public int getYM2612Port()
    {
        if (isYM2612Port0Write())
            return 0;
        if (isYM2612Port1Write())
            return 1;

        return -1;
    }

    public int getYM2612Register()
    {
        if (isYM2612Write())
            return Util.getInt8(data, 1);

        return -1;
    }

    public int getYM2612Value()
    {
        if (isYM2612Write())
            return Util.getInt8(data, 2);

        return -1;
    }

    public bool isYM2612KeyWrite()
    {
        return isYM2612Port0Write() && (getYM2612Register() == 0x28);
    }

    public bool isYM2612KeyOnWrite()
    {
        return isYM2612KeyWrite() && ((getYM2612Value() & 0xF0) == 0xF0);
    }

    public bool isYM2612KeyOffWrite()
    {
        return isYM2612KeyWrite() && ((getYM2612Value() & 0xF0) == 0x00);
    }

    // public int getYM2612KeyChannel()
    // {
    // if (isYM2612KeyWrite())
    // return getYM2612Value() & 0x7;
    //
    // return 0;
    // }

    public bool isYM26120x2XWrite()
    {
        return isYM2612Port0Write() && ((getYM2612Register() & 0xF0) == 0x20);
    }

    public bool isYM2612DACOn()
    {
        return isYM2612Port0Write() && ((getYM2612Register() & 0xFF) == 0x2B) && ((getYM2612Value() & 0x80) != 0);
    }

    public bool isYM2612DACOff()
    {
        return isYM2612Port0Write() && ((getYM2612Register() & 0xFF) == 0x2B) && ((getYM2612Value() & 0x80) == 0);
    }

    public bool isYM2612DACWrite()
    {
        return isYM2612Port0Write() && ((getYM2612Register() & 0xFF) == 0x2A);
    }

    public bool isYM2612CanIgnoreWrite()
    {
        if (isYM2612Port0Write())
            return (getYM2612Register() != 0x28) && (getYM2612Register() != 0x22) && (getYM2612Register() < 0x30);
        else if (isYM2612Port1Write())
            return (getYM2612Register() < 0x30);

        return false;
    }

    public bool isYM2612TimersWrite()
    {
        return isYM2612Port0Write() && (getYM2612Register() == 0x27);
    }

    public bool isYM2612TimersNoSpecialNoCSMWrite()
    {
        return isYM2612TimersWrite() && ((getYM2612Value() & 0xC0) == 0x00);
    }

    public bool isYM2612GeneralWrite()
    {
        return (isYM2612Port0Write() || isYM2612Port1Write()) && (getYM2612Register() < 0x30);
    }

    public bool isYM2612ChannelSet()
    {
        return (isYM2612Port0Write() || isYM2612Port1Write()) && (getYM2612Register() >= 0x30);
    }

    public bool isYM2612FreqWrite()
    {
        int port = getYM2612Port();

        if (port != -1)
        {
            int reg = getYM2612Register();
            return ((reg >= 0xA0) && (reg < 0xB0));
        }

        return false;
    }

    public int getYM2612Channel()
    {
        int port = getYM2612Port();

        if (port != -1)
        {
            int reg = getYM2612Register();
            int ch;

            // special key write command ?
            if ((port == 0) && (reg == 0x28))
            {
                int value = getYM2612Value();

                ch = value & 3;

                if (ch == 3)
                    return -1;

                ch += ((value & 4) != 0) ? 3 : 0;

                return ch;
            }
            // special case: channel 3 in special mode
            else if ((reg >= 0xA8) && (reg < 0xB0))
                ch = 2;
            // classic channel decoding
            else if ((reg >= 0x30) && (reg < 0xB8))
                ch = reg & 3;
            else
                return -1;

            if (ch != 3)
                return ch + (port * 3);
        }

        return -1;
    }
    public override int getChannel()
    {
        if (isYM2612Write())
            return getYM2612Channel();
        if (isPSGWrite())
            return getPSGChannel();

        return -1;
    }

    public int getYM2612PortChannel()
    {
        int ch = getYM2612Channel();

        if (ch == -1)
            return -1;
        if (ch >= 3)
            return ch - 3;
        return ch;
    }

    public bool isStream()
    {
        return isStreamControl() || isStreamData() || isStreamFrequency() || isStreamStart() || isStreamStartLong() || isStreamStop();
    }

    public bool isStreamControl()
    {
        return (getCommand() == STREAM_CONTROL);
    }

    public bool isStreamData()
    {
        return (getCommand() == STREAM_DATA);
    }

    public bool isStreamFrequency()
    {
        return (getCommand() == STREAM_FREQUENCY);
    }

    public bool isStreamStart()
    {
        return (getCommand() == STREAM_START);
    }

    public bool isStreamStartLong()
    {
        return (getCommand() == STREAM_START_LONG);
    }

    public bool isStreamStop()
    {
        return (getCommand() == STREAM_STOP);
    }

    public int getStreamId()
    {
        if (isStream())
            return Util.getInt8(data, 1);

        return -1;
    }

    public int getStreamBankId()
    {
        if (isStreamData())
            return Util.getInt8(data, 2);

        return -1;
    }

    public int getStreamBlockId()
    {
        if (isStreamStart())
            return Util.getInt8(data, 2);

        return -1;
    }

    public void setStreamBlockId(int value)
    {
        if (isStreamStart())
            Util.setInt8(data, 2, value);
    }

    public int getStreamFrenquency()
    {
        if (isStreamFrequency())
            return Util.getInt32(data, 2);

        return -1;
    }

    public int getStreamSampleAddress()
    {
        if (isStreamStartLong())
            return Util.getInt32(data, 2);

        return -1;
    }

    public void setStreamSampleAddress(int value)
    {
        if (isStreamStartLong())
            Util.setInt32(data, 2, value);
    }

    public int getStreamSampleSize()
    {
        if (isStreamStartLong())
            return Util.getInt32(data, 7);

        return -1;
    }

    public string getCommandDesc()
    {
        if (isStreamControl())
            return "Stream ctrl";
        if (isStreamData())
            return "Stream data";
        if (isStreamFrequency())
            return "Stream freq";
        if (isStreamStart())
            return "Stream start sh";
        if (isStreamStartLong())
            return "Stream start";
        if (isStreamStop())
            return "Stream stop";

        if (isDataBlock())
            return "Data block";
        if (isPCM())
            return "PCM";
        if (isSeek())
            return "PCM seek";
        if (isLoopStart())
            return "Loop start";
        if (isEnd())
            return "End";

        if (isPSGEnvWrite())
            return "PSG env #" + StringUtil.toHexaString(getPSGEnv(), 1);
        if (isPSGToneHighWrite())
            return "PSG tone high #" + StringUtil.toHexaString(getPSGFrequence(), 3);
        if (isPSGToneLowWrite())
            return "PSG tone low #" + StringUtil.toHexaString(getPSGFrequence(), 1);

        if (isShortWait())
            return "Short wait";
        if (isWait())
            return "Wait";

        if (isYM2612Write())
            return getYMCommandDesc(getYM2612Port(), getYM2612Register(), getYM2612Value());

        return "Others";
    }

    public static VGMCommand getKeyCommand(List<VGMCommand> commands, int channel)
    {
        foreach (VGMCommand command in commands)
            if (command.isYM2612KeyWrite() && (command.getYM2612Channel() == channel))
                return command;

        return null;
    }

    public static VGMCommand getKeyOffCommand(List<VGMCommand> commands, int channel)
    {
        foreach (VGMCommand command in commands)
            if (command.isYM2612KeyOffWrite() && (command.getYM2612Channel() == channel))
                return command;

        return null;
    }

    public static VGMCommand createYMCommand(int port, int reg, int value)
    {
        if (port == 0)
            return new VGMCommand(new byte[] {WRITE_YM2612_PORT0, (byte) reg, (byte) value});

        return new VGMCommand(new byte[] {WRITE_YM2612_PORT1, (byte) reg, (byte) value});
    }

    public static List<VGMCommand> createYMCommands(int port, int baseReg, int value)
    {
        List<VGMCommand> result = new();

        for (int ch = 0; ch < 3; ch++)
            for (int op = 0; op < 4; op++)
                result.Add(createYMCommand(port, baseReg + ((op & 3) << 2) + (ch & 3), value));

        return result;
    }

    public static List<VGMCommand> createWaitCommands(int wait)
    {
        List<VGMCommand> result = new();

        if (wait == 0x2DF)
            result.Add(new VGMCommand(WAIT_NTSC_FRAME));
        else if (wait == 0x372)
            result.Add(new VGMCommand(WAIT_PAL_FRAME));
        else
        {
            int remaining = wait;
            while (remaining > 0)
            {
                byte[] data = new byte[3];
                int w = Math.Min(65535, remaining);

                data[0] = 0x61;
                Util.setInt16(data, 1, w);

                result.Add(new VGMCommand(data));

                remaining -= w;
            }
        }

        return result;
    }
    public string toString()
    {
        return getCommandDesc();
    }

    public class LoopStartCommand : VGMCommand
    {
        public LoopStartCommand() : base(new byte[] {})
        {
        }
    }

    public class ComChannelSorter : IComparer<VGMCommand>
    {
        public int Compare(VGMCommand c1, VGMCommand c2)
        {
            return J.Compare(c1.getYM2612Channel(), c2.getYM2612Channel());
        }
    }

    public class ComChannelRegSorter : IComparer<VGMCommand>
    {
        public int Compare(VGMCommand c1, VGMCommand c2)
        {
            int result = J.Compare(c1.getYM2612Channel(), c2.getYM2612Channel());
            if (result == 0)
            {
                // freq registers ? high then low
                if (((c1.getYM2612Register() & 0xF4) == 0xA4) && (((c2.getYM2612Register() & 0xF4) == 0xA0)))
                    return -1;
                if (((c1.getYM2612Register() & 0xF4) == 0xA0) && (((c2.getYM2612Register() & 0xF4) == 0xA4)))
                    return 1;

                result = J.Compare(c1.getYM2612Register(), c2.getYM2612Register());
            }
            return result;
        }
    }

    public class LevelOFFCommLevelONSorter : IComparer<VGMCommand>
    {
        public int Compare(VGMCommand c1, VGMCommand c2)
        {
            int reg1 = c1.getYM2612Register();
            int reg2 = c2.getYM2612Register();
            int val1 = c1.getYM2612Value();
            int val2 = c2.getYM2612Value();

            // TL register ?
            if ((reg1 & 0XF0) == 0x40)
            {
                // TL OFF ? --> should be first
                if ((val1 & 0x7F) == 0x7F)
                    return -1;
                // TL ON ? --> should be last
                return 1;
            }
            // SL (D1L) register ?
            if ((reg1 & 0XF0) == 0x80)
            {
                // SL OFF ? --> should be first
                if ((val1 & 0xF0) == 0xF0)
                    return -1;
                // SL ON ? --> should be last
                return 1;
            }

            // TL register ?
            if ((reg2 & 0XF0) == 0x40)
            {
                // TL OFF ? --> should be first
                if ((val2 & 0x7F) == 0x7F)
                    return 1;
                // TL ON ? --> should be last
                return -1;
            }
            // SL (D1L) register ?
            if ((reg2 & 0XF0) == 0x80)
            {
                // SL OFF ? --> should be first
                if ((val2 & 0xF0) == 0xF0)
                    return 1;
                // SL ON ? --> should be last
                return -1;
            }

            // other command --> we don't care order
            return 0;
        }
    }

    public static ComChannelSorter channelSorter = new ComChannelSorter();
    public static ComChannelRegSorter channelRegSorter = new ComChannelRegSorter();
    public static LevelOFFCommLevelONSorter normalComSorter = new LevelOFFCommLevelONSorter();
}
