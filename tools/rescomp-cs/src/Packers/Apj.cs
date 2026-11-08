namespace Isdk.Rescomp.Packers;

/// <summary>
/// aPLib 圧縮 (SGDK の tools/apj の APJ.java をそのまま移したもの)。
/// 出力が rescomp.jar と 1 バイトも違わないように、Java 版の手順 (遅延して計算するコストの値、static な状態) まで同じにしている。
/// </summary>
public static class Apj
{
    sealed class Match
    {
        readonly byte[] data;
        int index;
        public int Offset;
        public int Length;
        int rawCost = -1;
        int cost = -1;
        int saved = -1;

        public Match(byte[] data, int index, int offset, int length)
        {
            this.data = data;
            this.index = index;
            Offset = offset;
            Length = length;
        }

        public void IncLength(int value)
        {
            index -= value;
            Length += value;
            rawCost = -1;
            cost = -1;
            saved = -1;
        }

        public bool IsShortOrLong()
        {
            if (Length >= 2 && Length <= 3 && Offset > 0 && Offset < 128) return true;
            return Length >= 3;
        }

        int ComputeRawCost()
        {
            int result = 0;
            for (int i = index; i < index + Length; i++)
                result += data[i] == 0 ? 7 : 9;
            return result;
        }

        int ComputeCost()
        {
            if (Length == 1 && Offset > 0 && Offset < 16)
                return 3 + 4;
            if (Length >= 2 && Length <= 3 && Offset > 0 && Offset < 128)
                return 3 + 8;
            if ((Length >= 2 && Offset >= 0x80 && Offset < 0x500) ||
                (Length >= 3 && Offset >= 0x500 && Offset < 0x7D00) ||
                Length >= 4)
            {
                int c = 2;
                if (!wasMatch && lastOffset == Offset)
                    c += 2;
                else
                {
                    c += (GetHighBitNum(Offset >> 8) * 2) + 2;
                    c += 8;
                }
                c += GetHighBitNum(Length) * 2;
                return c;
            }
            return RawCost;
        }

        public int RawCost { get { if (rawCost == -1) rawCost = ComputeRawCost(); return rawCost; } }
        public int Cost { get { if (cost == -1) cost = ComputeCost(); return cost; } }
        public int Saved { get { if (saved == -1) saved = RawCost - Cost; return saved; } }
    }

    readonly record struct ByteMatch(int Offset, int Repeat);

    // Java 版と同じく、コストの計算が読む static な状態
    static bool wasMatch;
    static int lastOffset;

    static Match FindBestMatch(List<ByteMatch>[] byteMatches, byte[] data, int ind)
    {
        if (ind < 1)
            return null;

        var bml = byteMatches[data[ind]];
        int curRepeat = GetRepeat(data, ind);
        int mInd = 0;
        Match best = null;
        int saved = 1;

        while (mInd < bml.Count)
        {
            var bm = bml[mInd];
            int off = bm.Offset;
            if (off >= ind)
                break;

            int repeat = bm.Repeat;
            if (repeat < curRepeat)
            {
                var match = new Match(data, ind, ind - off, repeat + 1);
                if (match.Saved >= saved)
                {
                    best = match;
                    saved = match.Saved;
                }
            }
            else
            {
                if (repeat > curRepeat)
                {
                    int delta = repeat - curRepeat;
                    if ((off + delta) >= ind)
                        delta = (ind - off) - 1;
                    off += delta;
                    repeat -= delta;
                }

                Match match;
                if (repeat > 0)
                {
                    if ((off + repeat) >= ind)
                        repeat = (ind - off) - 1;
                    match = GetMatch(data, off + repeat, ind + repeat);
                    match.IncLength(repeat);
                }
                else
                    match = GetMatch(data, off, ind);

                if (match.Saved >= saved)
                {
                    best = match;
                    saved = match.Saved;
                }
            }
            mInd++;
        }

        return best;
    }

    static Match GetMatch(byte[] data, int from, int ind)
    {
        int refOffset = from;
        int curOffset = ind;
        int len = 0;
        while (curOffset < data.Length && data[refOffset++] == data[curOffset++])
            len++;
        return new Match(data, ind, ind - from, len);
    }

    static int GetRepeat(byte[] data, int ind)
    {
        byte value = data[ind];
        int off = ind + 1;
        while (off < data.Length && data[off] == value)
            off++;
        return (off - ind) - 1;
    }

    static int GetLengthDelta(int offset)
    {
        if (offset < 0x80 || offset >= 0x7D00) return 2;
        if (offset >= 0x500) return 1;
        return 0;
    }

    static int GetHighBitNum(int value)
    {
        int result = 0;
        int v = value;
        if (v >= 0x10000) { result += 16; v >>= 16; }
        if (v >= 0x100) { result += 8; v >>= 8; }
        if (v >= 0x10) { result += 4; v >>= 4; }
        if (v >= 4) { result += 2; v >>= 2; }
        if (v >= 2) result++;
        return result;
    }

    sealed class BitWriter
    {
        readonly List<byte> buf;
        int offset = -1;
        int tag;
        int nbit;

        public BitWriter(int capacity) { buf = new List<byte>(Math.Max(capacity, 32)); }

        void SaveAndAllocateTag()
        {
            if (offset != -1)
                buf[offset] = (byte)tag;
            offset = buf.Count;
            buf.Add(0);
            tag = 0;
        }

        public void WriteBit(int bit)
        {
            if (nbit-- == 0)
            {
                SaveAndAllocateTag();
                nbit = 7;
            }
            tag |= (bit & 1) << nbit;
        }

        public void WriteUByte(int value) => buf.Add((byte)value);

        public byte[] ToArray()
        {
            if (offset != -1)
                buf[offset] = (byte)tag;
            return buf.ToArray();
        }
    }

    static void WriteVariableNumber(BitWriter stream, int value)
    {
        if (value < 2)
        {
            Console.WriteLine("Encoding error - writeVariableNumber");
            return;
        }
        int nbit = GetHighBitNum(value);
        nbit--;
        stream.WriteBit((value >> nbit) & 1);
        while (nbit-- > 0)
        {
            stream.WriteBit(1);
            stream.WriteBit((value >> nbit) & 1);
        }
        stream.WriteBit(0);
    }

    static void WriteFixedNumber(BitWriter stream, int value, int nbit)
    {
        for (int i = nbit - 1; i >= 0; i--)
            stream.WriteBit((value >> i) & 1);
    }

    static void WriteLiteral(BitWriter stream, int data)
    {
        stream.WriteBit(0);
        stream.WriteUByte(data);
        wasMatch = false;
    }

    static void WriteTinyBlock(BitWriter stream, int offset)
    {
        if (offset < 0 || offset > 15)
        {
            Console.WriteLine("Offset encoding error - writeTinyBlock");
            return;
        }
        stream.WriteBit(1);
        stream.WriteBit(1);
        stream.WriteBit(1);
        WriteFixedNumber(stream, offset, 4);
        wasMatch = false;
    }

    static void WriteShortBlock(BitWriter stream, int offset, int length)
    {
        if (offset < 1 || offset > 127)
        {
            Console.WriteLine("Offset encoding error - writeShortBlock");
            return;
        }
        if (length < 2 || length > 3)
        {
            Console.WriteLine("Length encoding error - writeShortBlock");
            return;
        }
        stream.WriteBit(1);
        stream.WriteBit(1);
        stream.WriteBit(0);
        stream.WriteUByte((offset << 1) | (length - 2));
        lastOffset = offset;
        wasMatch = true;
    }

    static void WriteBlock(BitWriter stream, int offset, int length)
    {
        stream.WriteBit(1);
        stream.WriteBit(0);

        if (!wasMatch && lastOffset == offset)
        {
            WriteVariableNumber(stream, 2);
            WriteVariableNumber(stream, length);
        }
        else
        {
            int highOffset = (offset >> 8) + 2;
            int lowOffset = offset & 0xFF;
            if (!wasMatch)
                highOffset++;
            WriteVariableNumber(stream, highOffset);
            stream.WriteUByte(lowOffset);
            WriteVariableNumber(stream, length - GetLengthDelta(offset));
        }

        lastOffset = offset;
        wasMatch = true;
    }

    static void WriteEndBlock(BitWriter stream)
    {
        stream.WriteBit(1);
        stream.WriteBit(1);
        stream.WriteBit(0);
        stream.WriteUByte(0);
    }

    /// <summary>aPLib で圧縮する (APJ.pack(data, ultra, true) と同じ)。</summary>
    public static byte[] Pack(byte[] data, bool ultra = false)
    {
        if (data.Length < 2)
            return data;

        var result = new BitWriter(data.Length);
        var byteMatches = new List<ByteMatch>[0x100];
        for (int i = 0; i < byteMatches.Length; i++)
            byteMatches[i] = new List<ByteMatch>();

        int mOffset = 0;
        while (mOffset < data.Length)
        {
            int off = mOffset;
            byte val = data[mOffset++];
            int repeat = 0;
            while (mOffset < data.Length && data[mOffset] == val)
            {
                repeat++;
                mOffset++;
            }
            byteMatches[val].Add(new ByteMatch(off, repeat));
        }
        // Java 版は Collections.sort (安定ソート) するが、もとから offset の順に並んでいる

        wasMatch = false;
        lastOffset = -1;

        var matches = new Match[data.Length];
        if (ultra)
        {
            for (int i = 1; i < matches.Length; i++)
                matches[i] = FindBestMatch(byteMatches, data, i);
        }
        else
        {
            for (int i = 1; i < matches.Length;)
            {
                var match = FindBestMatch(byteMatches, data, i);
                matches[i] = match;
                if (match != null)
                {
                    i += match.Length;
                    if (match.IsShortOrLong())
                    {
                        lastOffset = match.Offset;
                        wasMatch = true;
                    }
                    else
                        wasMatch = false;
                }
                else
                {
                    i++;
                    wasMatch = false;
                }
            }
        }

        if (ultra)
        {
            var costs = new int[matches.Length + 1];
            for (int i = matches.Length - 1; i >= 0; i--)
            {
                int literalCost = costs[i + 1] + (data[i] == 0 ? 7 : 9);
                int matchCost = int.MaxValue;
                var match = matches[i];
                if (match != null)
                    matchCost = match.Cost + costs[i + match.Length];
                if (literalCost < matchCost)
                {
                    costs[i] = literalCost;
                    matches[i] = null;
                }
                else
                    costs[i] = matchCost;
            }
        }

        lastOffset = 0;
        wasMatch = false;

        result.WriteUByte(data[0]);

        int ind = 1;
        while (ind < matches.Length)
        {
            bool done = false;
            var match = matches[ind];
            if (match != null)
            {
                int off = match.Offset;
                int l = match.Length;
                done = true;

                if (l == 1 && off > 0 && off < 16)
                    WriteTinyBlock(result, off);
                else if (l >= 2 && l <= 3 && off > 0 && off < 0x80)
                    WriteShortBlock(result, off, l);
                else if (l >= 2 && off >= 0x80 && off < 0x500)
                    WriteBlock(result, off, l);
                else if (l >= 3 && off >= 0x500 && off < 0x7D00)
                    WriteBlock(result, off, l);
                else if (l >= 4)
                    WriteBlock(result, off, l);
                else
                    done = false;

                if (done)
                    ind += l;
            }

            if (!done)
            {
                byte v = data[ind++];
                if (v == 0)
                    WriteTinyBlock(result, 0);
                else
                    WriteLiteral(result, v);
            }
        }

        WriteEndBlock(result);
        return result.ToArray();
    }
}
