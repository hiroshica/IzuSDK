namespace Isdk.Rescomp.Packers;

/// <summary>
/// LZ4W 圧縮 (SGDK の tools/lz4w の LZ4W.java をそのまま移したもの)。
/// </summary>
public static class Lz4w
{
    const int LITERAL_MAX_SIZE = 0xF;
    const int MATCH_MIN_SIZE = 1;
    const int MATCH_LONG_MIN_SIZE = 2;
    const int MATCH_MAX_SIZE = 0xF + MATCH_MIN_SIZE;
    const int MATCH_LONG_MAX_SIZE = 0xFF + MATCH_LONG_MIN_SIZE;
    const int MATCH_MIN_OFFSET = 1;
    const int MATCH_LONG_MIN_OFFSET = 1;
    const int MATCH_OFFSET_MAX = 0x0FF + MATCH_MIN_OFFSET;
    const int MATCH_LONG_OFFSET_MAX = 0x3FFF + MATCH_LONG_MIN_OFFSET;
    const int MATCH_LONG_OFFSET_MASK = 0x7FFF;
    const int MATCH_LONG_OFFSET_ROM_SOURCE_MASK = 0x8000;

    sealed class Match
    {
        public const int MAX_SAVED_WORD = MATCH_LONG_MAX_SIZE - 2;

        public readonly int CurOffset;
        public readonly int RefOffset;
        public readonly int Length;
        public readonly int SavedWord;
        public readonly int Cost;
        public readonly bool LongMatch;
        public readonly bool Rom;

        public Match(int curOff, int refOff, int len, bool fromRom)
        {
            CurOffset = curOff;
            RefOffset = refOff;
            Length = len;
            Rom = fromRom;

            if (fromRom || RelativeOffset > MATCH_OFFSET_MAX || len > MATCH_MAX_SIZE)
            {
                LongMatch = true;
                Cost = 2;
                if (fromRom && RelativeOffset > (MATCH_LONG_OFFSET_MAX - 0x20))
                    SavedWord = 0;
                else
                    SavedWord = Math.Max(0, len - MATCH_LONG_MIN_SIZE);
            }
            else
            {
                LongMatch = false;
                Cost = 1;
                SavedWord = len - MATCH_MIN_SIZE;
            }
        }

        public Match() : this(0, 0, 0, false) { }

        public int RelativeOffset => Math.Abs(CurOffset - RefOffset);
    }

    readonly record struct WordMatch(int Offset, int Repeat);

    /// <summary>
    /// LZ4W で圧縮する (LZ4W.pack(data, start, true) と同じ)。data の start バイト目より前は「前のデータ」として参照だけする。
    /// 前のデータを使えないときは ArgumentException (Java 版の IllegalArgumentException)。
    /// </summary>
    public static byte[] Pack(byte[] data, int start)
    {
        var result = new List<byte>(data.Length + 16);
        var literal = new List<byte>(1024);

        int len = data.Length - start;

        if (len >= 2)
        {
            var wdata = ByteToWordLE(data);
            int offset = start / 2;

            var wordMatches = new List<WordMatch>[0x10000];
            for (int i = 0; i < wordMatches.Length; i++)
                wordMatches[i] = new List<WordMatch>();

            int wmOffset = Math.Max(offset - MATCH_LONG_OFFSET_MAX, 0);
            while (wmOffset < wdata.Length)
            {
                int off = wmOffset;
                ushort val = wdata[wmOffset++];
                int repeat = 0;
                while (wmOffset < wdata.Length && wdata[wmOffset] == val)
                {
                    repeat++;
                    wmOffset++;
                }
                wordMatches[val].Add(new WordMatch(off, repeat));
            }

            var matches = new Match[wdata.Length - offset];
            for (int i = 0; i < matches.Length; i++)
                matches[i] = FindBestMatch(wordMatches, wdata, offset + i, start / 2);

            var costs = new int[matches.Length + 1];
            for (int i = matches.Length - 1; i >= 0; i--)
            {
                int literalCost = costs[i + 1] + 1;
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

            literal.Clear();
            int ind = 0;
            int offAdj = 0;
            while (ind < matches.Length)
            {
                var match = matches[ind];
                if (match != null)
                {
                    offAdj += AddSegment(result, literal, match, offAdj);
                    ind += match.Length;
                    literal.Clear();
                }
                else
                    WriteWordLE(literal, wdata[offset + ind++]);
            }
        }

        if (literal.Count > 0)
        {
            AddSegment(result, literal, new Match(), 0);
            literal.Clear();
        }

        // 終わりの印
        AddSegment(result, literal, new Match(), 0);

        if ((len & 1) != 0)
        {
            result.Add(0x80);
            result.Add(data[data.Length - 1]);
        }
        else
        {
            result.Add(0x00);
            result.Add(0x00);
        }

        return result.ToArray();
    }

    static Match FindBestMatch(List<WordMatch>[] wordMatches, ushort[] wdata, int ind, int originStartOffset)
    {
        if (ind < 1)
            return null;

        var wml = wordMatches[wdata[ind]];
        int offMin = Math.Max(0, ind - MATCH_LONG_OFFSET_MAX);

        int wmlInd = 0;
        while (wmlInd < wml.Count)
        {
            var wm = wml[wmlInd];
            if ((wm.Offset + wm.Repeat) >= offMin)
                break;
            wmlInd++;
        }

        int curRepeat = GetRepeat(wdata, ind);
        Match best = null;
        int savedWord = 1;

        while (wmlInd < wml.Count)
        {
            var wm = wml[wmlInd];
            int off = wm.Offset;
            int repeat = wm.Repeat;

            if (off >= ind)
                break;

            if (off < offMin)
            {
                repeat -= offMin - off;
                off = offMin;
            }

            if (off >= originStartOffset || (off + repeat) < originStartOffset)
            {
                if (repeat < curRepeat)
                {
                    var match = new Match(ind, off, Math.Min(MATCH_LONG_MAX_SIZE, repeat + 1), off < originStartOffset);
                    if (match.SavedWord >= savedWord)
                    {
                        best = match;
                        savedWord = match.SavedWord;
                        if (savedWord == Match.MAX_SAVED_WORD)
                            return best;
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
                        match = FindBestMatchInternal(wdata, off + repeat, ind + repeat, originStartOffset);
                        match = new Match(match.CurOffset - repeat, match.RefOffset - repeat,
                                Math.Min(MATCH_LONG_MAX_SIZE, match.Length + repeat),
                                (match.RefOffset - repeat) < originStartOffset);
                    }
                    else
                        match = FindBestMatchInternal(wdata, off, ind, originStartOffset);

                    if (match.SavedWord >= savedWord)
                    {
                        best = match;
                        savedWord = match.SavedWord;
                        if (savedWord == Match.MAX_SAVED_WORD)
                            return best;
                    }
                }
            }
            else
            {
                while ((repeat-- >= 0) && (off < ind))
                {
                    var match = FindBestMatchInternal(wdata, off, ind, originStartOffset);
                    if (match.SavedWord >= savedWord)
                    {
                        best = match;
                        savedWord = match.SavedWord;
                        if (savedWord == Match.MAX_SAVED_WORD)
                            return best;
                    }
                    off++;
                }
            }

            wmlInd++;
        }

        return best;
    }

    static Match FindBestMatchInternal(ushort[] wdata, int from, int ind, int originStart)
    {
        int maxLen = from < originStart ? Math.Min(originStart - from, MATCH_LONG_MAX_SIZE) : MATCH_LONG_MAX_SIZE;
        int refOffset = from;
        int curOffset = ind;
        int len = 0;
        while (curOffset < wdata.Length && wdata[refOffset++] == wdata[curOffset++] && len < maxLen)
            len++;
        return new Match(ind, from, len, from < originStart);
    }

    static int GetRepeat(ushort[] wdata, int ind)
    {
        ushort value = wdata[ind];
        int off = ind + 1;
        while (off < wdata.Length && wdata[off] == value)
            off++;
        return (off - ind) - 1;
    }

    static int AddSegment(List<byte> result, List<byte> literal, Match match, int offsetDiff)
    {
        var literalArray = literal;
        int literalLength = literal.Count / 2;
        int literalOffset = 0;
        int offsetAdj = 0;

        while (literalLength > LITERAL_MAX_SIZE)
        {
            offsetAdj++;
            result.Add((byte)(((LITERAL_MAX_SIZE & 0xF) << 4) | 0));
            result.Add(0);
            for (int i = 0; i < LITERAL_MAX_SIZE * 2; i++)
                result.Add(literalArray[literalOffset + i]);
            literalOffset += LITERAL_MAX_SIZE * 2;
            literalLength -= LITERAL_MAX_SIZE;
        }

        int matchLength = match.Length;
        int matchOffset = match.RelativeOffset;

        if (matchLength > 0 || literalLength > 0)
        {
            if (match.LongMatch)
            {
                offsetAdj += 2;
                offsetAdj -= matchLength;
                result.Add((byte)(((literalLength & 0xF) << 4) | 0));
                result.Add((byte)(matchLength - MATCH_LONG_MIN_SIZE));
            }
            else
            {
                offsetAdj++;
                if (matchLength != 0)
                {
                    offsetAdj -= matchLength;
                    result.Add((byte)(((literalLength & 0xF) << 4) | ((matchLength - MATCH_MIN_SIZE) & 0xF)));
                    result.Add((byte)(matchOffset - MATCH_MIN_OFFSET));
                }
                else
                {
                    result.Add((byte)(((literalLength & 0xF) << 4) | 0));
                    result.Add(0);
                }
            }

            for (int i = 0; i < literalLength * 2; i++)
                result.Add(literalArray[literalOffset + i]);

            if (match.LongMatch)
            {
                if (match.Rom)
                {
                    matchOffset += offsetDiff + offsetAdj + matchLength;
                    if (matchOffset > MATCH_LONG_OFFSET_MAX)
                        throw new ArgumentException("Can't encode long offset... retry without previous data block !");
                }

                matchOffset = (-(matchOffset - MATCH_LONG_MIN_OFFSET)) & MATCH_LONG_OFFSET_MASK;
                if (match.Rom)
                    matchOffset |= MATCH_LONG_OFFSET_ROM_SOURCE_MASK;

                result.Add((byte)(matchOffset >> 8));
                result.Add((byte)(matchOffset & 0xFF));
            }
        }
        else
        {
            result.Add(0);
            result.Add(0);
        }

        return offsetAdj;
    }

    static ushort[] ByteToWordLE(byte[] data)
    {
        int len = data.Length / 2;
        var result = new ushort[len];
        for (int i = 0; i < len; i++)
            result[i] = (ushort)(data[2 * i] | (data[2 * i + 1] << 8));
        return result;
    }

    static void WriteWordLE(List<byte> dst, int value)
    {
        dst.Add((byte)value);
        dst.Add((byte)(value >> 8));
    }
}
