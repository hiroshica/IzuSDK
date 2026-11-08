using System.Text;
namespace Isdk.Xgm2Tool;




public abstract class Command
{
    public static readonly Comparison<Command> timeComparator = (c1, c2) => J.Compare(c1.time, c2.time);

    public static bool contains(IReadOnlyList<Command> commands, Command command)
    {
        foreach (Command c in commands)
            if (c.isSame(command))
                return true;

        return false;
    }

    public static int getDataSize(IReadOnlyList<Command> commands)
    {
        int result = 0;

        foreach (Command c in commands)
            result += c.size;

        return result;
    }

    /**
     * Return index of the command at given time
     */
    public static int getCommandIndexAtTime(IReadOnlyList<Command> commands, int time)
    {
        // we could do that a lot faster by using dichotomy but we don't care
        for (int c = 0; c < commands.Count; c++)
            if (commands[c].time >= time)
                return c;

        return commands.Count - 1;
    }

    /**
     * Return command at given vgmTime
     */
    public static Command getCommandAtTime(IReadOnlyList<Command> commands, int time)
    {
        int ind = getCommandIndexAtTime(commands, time);

        if (ind >= 0)
            return commands[ind];

        return null;
    }

    /**
     * Return command index at given origin offset
     */
    public static int getCommandIndexAtOffset(IReadOnlyList<Command> commands, int offset)
    {
        for (int c = 0; c < commands.Count; c++)
            if (commands[c].getOriginOffset() == offset)
                return c;

        return -1;
    }

    /**
     * Return command index at given origin offset
     */
    public static Command getCommandAtOffset(IReadOnlyList<Command> commands, int offset)
    {
        int ind = getCommandIndexAtOffset(commands, offset);

        if (ind >= 0)
            return commands[ind];

        return null;
    }

    /**
     * Update origin offset information for all command
     */
    public static void computeOffsets(IReadOnlyList<Command> commands, int startOffset)
    {
        int off = startOffset;

        foreach (Command com in commands)
        {
            com.originOffset = off;
            off += com.size;
        }
    }

    public static string getYMCommandDesc(int port, int reg, int value)
    {
        string res;

        if (reg < 0x20)
            return "Unknow YM write";

        if (reg < 0x30)
        {
            switch (reg & 0x0F)
            {
                case 2:
                    return "Set LFO";
                case 4:
                    return "Timer A MSB";
                case 5:
                    return "Timer A LSB";
                case 6:
                    return "Timer B";
                case 7:
                    return "CH2 mode / timer reset";
                case 8:
                    if ((value & 0xF0) == 0)
                        res = "KEY OFF";
                    else
                        res = "KEY ON";
                    return res + " CH" + (((value & 0x0F) >= 4) ? (value & 3) + 3 : (value & 3));
                case 0xA:
                    return "DAC value";
                case 0xB:
                    return "DAC enable";
                default:
                    return "Unknow YM write";
            }
        }
        else if (reg >= 0xA0)
        {
            if ((reg >= 0xA8) && (reg < 0xB0))
                res = "CH" + ((port > 0) ? "5" : "2") + " OP" + ((reg & 3) + 1) + " - ";
            else
                res = "CH" + ((port > 0) ? (reg & 3) + 3 : (reg & 3)) + " - ";

            switch (reg & 0xFC)
            {
                case 0xA0:
                    return res + "FREQ LSB";
                case 0xA4:
                    return res + "FREQ MSB";
                case 0xA8:
                    return res + "FREQ LSB (SPE)";
                case 0xAC:
                    return res + "FREQ MSB (SPE)";
                case 0xB0:
                    return res + "Feedback / AlgoR";
                case 0xB4:
                    return res + "PAN / AMS / FMS";
                default:
                    return res + "Unknow CH set";
            }
        }
        else
        {
            res = "CH" + ((port > 0) ? (reg & 3) + 3 : (reg & 3)) + " " + "OP" + ((reg >> 2) & 3) + " - ";

            switch (reg & 0xF0)
            {
                case 0x30:
                    return res + "DT / MUL";
                case 0x40:
                    return res + "TL";
                case 0x50:
                    return res + "RS / AR";
                case 0x60:
                    return res + "AM / D1R";
                case 0x70:
                    return res + "D2R";
                case 0x80:
                    return res + "SL / RR";
                case 0x90:
                    return res + "SSG-EG";
                default:
                    return res + "Unknow slot set";
            }
        }
    }

    public byte[] data;
    public int originOffset;
    /**
     * Time is expressed in 1/44100 of second (VGM format)
     */
    public int time;
    /**
     * Size of the command in byte
     */
    public int size;

    public Command(byte[] data, int time)
    {
        this.data = data;
        this.time = time;

        // not yet computed
        size = 0;
        originOffset = 0;
    }

    public Command(byte[] data) : this(data, 0)
    {
    }

    public Command(int command, int time) : this(new byte[] {(byte) command}, time)
    {

        size = 1;
    }

    public Command(int command) : this(new byte[] {(byte) command}, 0)
    {

        size = 1;
    }

    public int getCommand()
    {
        return Util.getInt8(data, 0);
    }

    public int getOriginOffset()
    {
        return originOffset;
    }

    public abstract int getChannel();

    /**
     * Return time of this command in ms
     */
    public int getMilliSecond()
    {
        return (int) ((time * 1000L) / 44100L);
    }

    /**
     * Return time of this command in frame (PAL or NTSC depending the argument)
     */
    public int getFrame(bool pal)
    {
        if (pal)
            return time / 882;

        return time / 735;
    }

    public bool isSame(Command com)
    {
        // compare data
        return J.ArrayEquals(data, com.data);
    }

    public byte[] asByteArray()
    {
        return data;
    }

    public string toHexaString()
    {
        return Util.getBytesAsHexaString(data, 0, size, 32);
    }
    public string toString()
    {
        return toHexaString();
    }
}
