using System.Text;
namespace Sgdk.Xgm2Tool;




public class XGCPacker
{
    public const int LITERAL_MAX_SIZE = 7;
    public const int MATCH_MAX_SIZE = 31;
    public const int MATCH_OFFSET_MAX = 0x100;

    public const int FRAME_MIN_SIZE = 32;
    public const int FRAME_MAX_SIZE = 256;
    // to lower unpacking time
    // public const int FRAME_MAX_SIZE = 128;

    public class DynamicByteArray : ByteArrayOutputStream
    {
        public DynamicByteArray()
        {
        }

        public DynamicByteArray(int size) : base(size)
        {
        }

        public byte read(int off)
        {
            if ((off < 0) || (off >= count))
                throw new IndexOutOfRangeException();

            return buf[off];
        }
    }

    
    public class ByteMatchList : List<ByteMatch>
    {
        public ByteMatchList()
        {
        }
    }

    public class ByteMatch : IComparable<ByteMatch>
    {
        public int offset;
        public int repeat;

        public ByteMatch(int offset, int repeat)
        {
            this.offset = offset;
            this.repeat = repeat;
        }
        public string toString()
        {
            return "Off=" + offset + " - Repeat=" + repeat;
        }
        public int CompareTo(ByteMatch bm)
        {
            return J.Compare(offset, bm.offset);
        }
    }

    public class Match
    {
        public const int MAX_SAVED = MATCH_MAX_SIZE - 1;

        // current offset (offset of what we want to compress)
        public int curOffset;
        // reference offset
        public int refOffset;

        public int length;
        public int saved;
        public int cost;

        public Match(int curOff, int refOff, int len)
        {
            curOffset = curOff;
            refOffset = refOff;
            length = len;
            cost = 2;
            saved = len - cost;
        }

        /**
         * @return relative offset (positive)
         */
        public int getRelativeOffset()
        {
            return Math.Abs(curOffset - refOffset);
        }

        public bool fixForExtraByte(int offset)
        {
            int relativeOffset = getRelativeOffset();

            // impacted ?
            if (offset < relativeOffset)
            {
                // get maximum allowed len for match
                int maxLen = relativeOffset - offset;

                // above max len ? --> cannot be used
                if (length >= maxLen)
                {
                    if (Launcher.verbose)
                        Console.WriteLine("Info: cannot use match (len = " + length + ") because of delay frame command.");

                    return false;
                }

                // fix offset
                refOffset--;
            }

            // not impacted
            return true;
        }
        public string toString()
        {
            return J.Format("off=%4X, len=%d saved=%d", (int)(getRelativeOffset()), (int)(length), (int)(saved));
        }

    }

    public static int[] literalLen = new int[LITERAL_MAX_SIZE + 1];
    public static int[] matchLen = new int[MATCH_MAX_SIZE + 1];
    public static int[] matchOffset = new int[MATCH_OFFSET_MAX + 2];

    public static int getRepeat(byte[] data, int ind)
    {
        byte value = data[ind];

        int off = ind + 1;
        while ((off < data.Length) && (data[off] == value))
            off++;

        return (off - ind) - 1;
    }

    public static Match findBestMatchInternal(byte[] data, int from, int ind)
    {
        int refOffset;
        int curOffset;
        int len;

        // test on simple copy
        refOffset = from;
        curOffset = ind;
        len = 0;
        while ((curOffset < data.Length) && (data[refOffset++] == data[curOffset++]) && (len < MATCH_MAX_SIZE))
            len++;

        return new Match(ind, from, len);
    }

    public static Match findBestMatch(ByteMatchList[] byteMatches, byte[] data, int ind)
    {
        // nothing we can do
        if (ind < 1)
            return null;

        // get word matches for current word
        ByteMatchList bml = byteMatches[data[ind] & 0xFF];
        // window search size = 255
        int offMin = Math.Max(0, ind - MATCH_OFFSET_MAX);

        // find starting index in word match list
        int bmlInd = 0;
        while (bmlInd < bml.Count)
        {
            ByteMatch wm = bml[bmlInd];

            // accepted offset ? --> start here
            if ((wm.offset + wm.repeat) >= offMin)
                break;

            bmlInd++;
        }

        // get number of repeat for current byte
        int curRepeat = getRepeat(data, ind);

        Match best = null;
        int saved = 0;

        // for all accepted matches
        while (bmlInd < bml.Count)
        {
            ByteMatch wm = bml[bmlInd];

            int off = wm.offset;
            int repeat = wm.repeat;

            // raised current offset ? --> stop now
            if (off >= ind)
                break;

            // need to adjust start ?
            if (off < offMin)
            {
                repeat -= offMin - off;
                off = offMin;
            }

            // can optimize repeat ?
            if ((off >= 0) || ((off + repeat) < 0))
            {
                // less repeat on match
                if (repeat < curRepeat)
                {
                    Match match = new Match(ind, off, Math.Min(MATCH_MAX_SIZE, repeat + 1));

                    // use >= as we always prefer match to literal
                    if (match.saved >= saved)
                    {
                        best = match;
                        saved = match.saved;

                        // maximum saved ? --> don't continue
                        if (saved >= Match.MAX_SAVED)
                            return best;
                    }
                }
                else
                {
                    // more repeat on match ?
                    if (repeat > curRepeat)
                    {
                        // bypass extras repeats on match
                        int delta = repeat - curRepeat;
                        // fix maximum delta to not raise ind
                        if ((off + delta) >= ind)
                            delta = (ind - off) - 1;

                        off += delta;
                        repeat -= delta;
                    }

                    Match match;

                    // still some repeat ?
                    if (repeat > 0)
                    {
                        // we raised ind ? --> limit start offset to (ind - 1)
                        if ((off + repeat) >= ind)
                            repeat = (ind - off) - 1;

                        // easy optimization (start comparing after repeat)
                        match = findBestMatchInternal(data, off + repeat, ind + repeat);
                        // then fix the match
                        match = new Match(match.curOffset - repeat, match.refOffset - repeat, Math.Min(MATCH_MAX_SIZE, match.length + repeat));
                    }
                    else
                        match = findBestMatchInternal(data, off, ind);

                    // use >= as we always prefer match to literal
                    if (match.saved >= saved)
                    {
                        best = match;
                        saved = match.saved;

                        // maximum saved ? --> don't continue
                        if (saved == Match.MAX_SAVED)
                            return best;
                    }
                }
            }
            else
            {
                // for each repeated byte
                while ((repeat-- >= 0) && (off < ind))
                {
                    Match match = findBestMatchInternal(data, off, ind);

                    // use >= as we always prefer match to literal
                    if (match.saved >= saved)
                    {
                        best = match;
                        saved = match.saved;

                        // maximum saved ? --> don't continue
                        if (saved == Match.MAX_SAVED)
                            return best;
                    }

                    off++;
                }
            }

            // next word match
            bmlInd++;
        }

        return best;
    }

    public static int getMaxFrameOffsetFor(List<int> frameOffsets, int startInd, int curOffset)
    {
        int ind = startInd;
        int maxOff = curOffset + FRAME_MAX_SIZE;

        while ((ind < frameOffsets.Count) && (frameOffsets[ind] < maxOff))
            ind++;

        return frameOffsets[Math.Max(--ind, startInd)];
    }

    public static void addBlock(ByteArrayOutputStream result, byte[] literal, Match match, int baseAlign)
    {
        byte[] literalData = literal;
        int start = result.size();
        int blockSize = literalData.Length + ((match != null) ? 2 : 1);
        int end = start + (blockSize - 1);

        literalLen[literalData.Length]++;

        // crossing page (need to split) ?
        if (((start + baseAlign) & 0xFF00) != ((end + baseAlign) & 0xFF00))
        {
            // remaining byte in first page
            int firstPageRemain = 0x100 - ((start + baseAlign) & 0xFF);

            // write first part of literal data
            if (firstPageRemain > 1)
            {
                int len = firstPageRemain - 1;

                // write block header with page cross marker
                result.write((len << 5) | 1);
                // write literal data
                result.write(literalData, 0, len);

                // remaining literal data
                literalData = J.CopyOfRange(literalData, len, literalData.Length);
            }
            else
                // just write cross page marker
                result.write(1);
        }

        // match type data block
        if (match != null)
        {
            int off = match.getRelativeOffset();

            matchLen[match.length]++;
            matchOffset[off]++;

            // literal + match length
            result.write((literalData.Length << 5) | match.length);
            // copy literal data if any
            result.write(literalData);
            // write match offset *after* literal data (simpler for unpacking)
            result.write((-off) & 0xFF);

        }
        // literal only data block
        else
        {
            matchLen[0]++;

            // write literal data only if not empty
            if (literalData.Length > 0)
            {
                // literal header
                result.write(literalData.Length << 5);
                // copy literal data
                result.write(literalData);
            }
        }
    }

    public static void addBlock(ByteArrayOutputStream result, ByteArrayOutputStream literal, Match match, int baseAlign)
    {
        addBlock(result, literal.toByteArray(), match, baseAlign);
        // reset literal buffer
        literal.reset();
    }

    public static byte[] pack(byte[] data, List<int> frameOffsets, int baseAlign)
    {
        if ((data == null) || (data.Length == 0))
            return new byte[0];

        ByteArrayOutputStream result = new ByteArrayOutputStream();
        ByteArrayOutputStream literal = new ByteArrayOutputStream();

        // PASS 1: build the byte matches table
        ByteMatchList[] byteMatches = new ByteMatchList[0x100];
        for (int i = 0; i < byteMatches.Length; i++)
            byteMatches[i] = new ByteMatchList();

        // current offset to start counting matches
        int mOffset = 0;
        while (mOffset < data.Length)
        {
            int off = mOffset;
            byte val = data[mOffset++];

            int repeat = 0;
            while ((mOffset < data.Length) && (data[mOffset] == val))
            {
                repeat++;
                mOffset++;
            }

            // need to unsign val / add new byte match
            byteMatches[val & 0xFF].Add(new ByteMatch(off, repeat));
        }

        // sort all ByteMatch list
        for (int i = 0; i < byteMatches.Length; i++)
            J.Sort(byteMatches[i]);

        // PASS 2: get best match for each source position using the word matches table
        Match[] matches = new Match[data.Length];

        for (int i = 0; i < matches.Length; i++)
            matches[i] = findBestMatch(byteMatches, data, i);

        // PASS 3: walk backward in matches and find optimal match length
        int[] costs = new int[matches.Length + 1];

        // initialize ending cost
        costs[matches.Length] = 0;

        for (int i = matches.Length - 1; i >= 0; i--)
        {
            // literal cost = next cost + 1
            int literalCost = costs[i + 1] + 1;
            // default match cost
            int matchCost = int.MaxValue;

            Match match = matches[i];

            // we have a match ? its cost = current match cost + cost after match sequence
            if (match != null)
                matchCost = match.cost + costs[i + match.length];

            // literal cost is cheaper than match cost ?
            if (literalCost < matchCost)
            {
                // change the match to a literal as it is more efficient
                costs[i] = literalCost;
                matches[i] = null;
            }
            else
                costs[i] = matchCost;
        }

        // PASS 4: build compressed data from optimal matches
        J.Fill(literalLen, 0);
        J.Fill(matchLen, 0);
        J.Fill(matchOffset, 0);

        literal.reset();
        int nextFrameOffset = frameOffsets[0];
        int maxNextFrameOffset = Math.Max(getMaxFrameOffsetFor(frameOffsets, 0, 0), nextFrameOffset);
        int ind = 0;
        int lastFrameStart = 0;
        int frameInd = 1;
        while (ind < matches.Length)
        {
            int frameSize = ind - lastFrameStart;
            Match match = matches[ind];
            int blockSize = (match != null) ? match.length : 1;

            // get next frame start offset
            while ((nextFrameOffset < ind) && (frameInd < frameOffsets.Count))
                nextFrameOffset = frameOffsets[frameInd++];

            // !! fatal error (should never happen) !!
            if (frameSize >= FRAME_MAX_SIZE)
                throw new Exception("Error: max frame size reached at frame #" + (frameInd - 1));
            
            // aligned on the end of frame ?
            bool accept = (ind == nextFrameOffset);
            if (accept) 
            {
                // frame size is big enough or we reached maximum allowed offset ?
                accept &= (frameSize >= FRAME_MIN_SIZE) || (ind == maxNextFrameOffset); 
                
                if (accept)
                {
                    if (frameSize > 128)
                    {
                        if (Launcher.verbose)
                            Console.WriteLine("Warning: large frame at #" + (frameInd - 1) + " - size = " + frameSize);
                    }
    
                    // literal not empty ? --> add literal block first
                    if (literal.size() > 0)
                        addBlock(result, literal, null, baseAlign);
    
                    // add end frame unpack block marker
                    result.write(0);
                    lastFrameStart = ind;
    
                    // get max next end frame offset
                    maxNextFrameOffset = getMaxFrameOffsetFor(frameOffsets, frameInd, ind);
                    // get next end frame offset
                    while ((nextFrameOffset <= ind) && (frameInd < frameOffsets.Count))
                        nextFrameOffset = frameOffsets[frameInd++];
                }
            }
            
            // was not accepted ?
            if (!accept)
            {
                // above max frame offset ? --> cancel match so we can stop on next frame
                if ((ind + blockSize) > maxNextFrameOffset)
                {
                    if (Launcher.verbose)
                        Console.WriteLine("Info: cannot use match at offset =" + ind + " (len = " + blockSize + ") because of frame end alignement");

                    match = null;
                }
            }

            // match found ?
            if (match != null)
            {
                // add a new block
                addBlock(result, literal, match, baseAlign);
                // next ind
                ind += match.length;
            }
            // no match found --> add to literal buffer
            else
            {
                // max size for literal ? --> flush it
                if (literal.size() >= LITERAL_MAX_SIZE)
                    addBlock(result, literal, null, baseAlign);

                // write to literal and pass to next ind
                literal.write(data[ind++]);
            }
        }

        // literal not empty ? --> add last literal block
        if (literal.size() > 0)
            addBlock(result, literal, null, baseAlign);

        // end marker
        result.write(0);

        if (Launcher.verbose)
        {
            Console.WriteLine("XGC block compressed to " + result.size() + " bytes (original = " + data.Length + ")");
            Console.WriteLine("Compression stats:");
            Console.Write("\t\t");
            for (int i = 0; i < Math.Max(Math.Max(LITERAL_MAX_SIZE + 1, MATCH_MAX_SIZE + 1), MATCH_OFFSET_MAX + 2); i++)
                Console.Write(i + "\t");
            Console.WriteLine();
            Console.Write("Literals:\t");
            for (int i = 0; i < LITERAL_MAX_SIZE + 1; i++)
                Console.Write(literalLen[i] + "\t");
            Console.WriteLine();
            Console.Write("Matches:\t");
            for (int i = 0; i < MATCH_MAX_SIZE + 1; i++)
                Console.Write(matchLen[i] + "\t");
            Console.WriteLine();
            Console.Write("Offsets:\t");
            for (int i = 0; i < MATCH_OFFSET_MAX + 2; i++)
                Console.Write(matchOffset[i] + "\t");
            Console.WriteLine();
        }

        return result.toByteArray();
    }

    public static byte[] unpack(byte[] data, byte[] verif)
    {
        DynamicByteArray result = new DynamicByteArray();

        bool end = false;
        int ind = 0;
        while (ind < data.Length)
        {
            byte block = data[ind++];
            int litSize = (block >> 5) & 0x07;
            int matSize = (block >> 0) & 0x1F;

            // get offset
            int matOff = (matSize > 1) ? (data[ind++] & 0xFF) : 0;

            // write literal
            for (int i = 0; i < litSize; i++)
            {
                result.write(data[ind++]);
                if ((verif != null) && (verif[result.size() - 1] != result.read(result.size() - 1)))
                    Console.WriteLine("Error while unpacking - offset = " + (result.size() - 1));
            }

            if (matSize > 1)
            {
                int off = result.size() - matOff;
                for (int i = 0; i < matSize; i++)
                {
                    result.write(result.read(off++));
                    if ((verif != null) && (verif[result.size() - 1] != result.read(result.size() - 1)))
                        Console.WriteLine("Error while unpacking - offset = " + (result.size() - 1));
                }
            }
        }

        if (Launcher.verbose)
            Console.WriteLine("Unpacked = " + result.size() + " - packed = " + data.Length);

        return result.toByteArray();
    }
}
