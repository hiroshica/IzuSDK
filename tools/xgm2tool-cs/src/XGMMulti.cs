using System.Text;
namespace Isdk.Xgm2Tool;




public class XGMMulti
{
    // multi XGM file supports a maximum of 252-1 = 251 samples (that don't let much free slots to SFX when 251 samples
    // are used)

    public List<XGM> xgms;
    public List<XGMSample> sharedSamples;

    public bool pal;
    public bool hasGD3;
    public bool packed;

    // just for user information
    public int mergedSample;

    public XGMMulti(List<XGM> xgms, bool pack)
    {
        if (!Launcher.silent)
            Console.WriteLine("Converting " + xgms.Count + " XGM to multi tracks XGM...");

        this.xgms = xgms;
        sharedSamples = new();
        packed = pack;

        if (xgms.Count > 128)
        {
            Console.Error.WriteLine("Warning: multi tracks XGM is limited to 128 tracks max (" + (xgms.Count - 128) + " tracks will be ignored)!");

            // remove all tracks above 128
            while (xgms.Count > 128)
                xgms.remove(xgms.Count - 1);
        }

        hasGD3 = false;
        bool? p = null;

        // set pal / hasGD3 flags
        foreach (XGM xgm in xgms)
        {
            if (p == null)
                p = (bool)(xgm.pal);
            else if (p != xgm.pal)
            {
                Console.Error.WriteLine("Warning: multi tracks XGM cannot mix PAL and NTSC tracks");
                // select NTSC by default
                p = false;
            }

            if ((xgm.gd3 != null) || (xgm.xd3 != null))
                hasGD3 = true;
        }

        // set pal
        pal = (p != null) ? p.Value : false;

        // build shared samples and update XGM according
        mergedSample = 0;
        foreach (XGM xgm in xgms)
        {
            HashSet<XGMFMCommand> updatedCommands = new();
            foreach (XGMSample sample in xgm.samples)
                mergeSample(sample, xgm, updatedCommands);

            xgm.rebuildFMCommands();
        }

        if (!Launcher.silent)
        {
            try
            {
                Console.WriteLine("FM data size: " + getTotalFMMusicDataSize());
                Console.WriteLine("PSG data size: " + getTotalPSGMusicDataSize());
            }
            catch (IOException e)
            {
                //
            }

            Console.WriteLine("PCM data size: " + getPCMDataSize());
            Console.WriteLine("Number of sample = " + sharedSamples.Count + " (" + mergedSample + " merged samples)");
        }
    }

    public XGMSample findMatchingSample(XGMSample sample)
    {
        XGMSample bestMatch = null;
        double bestScore = 0d;

        foreach (XGMSample s in sharedSamples)
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

    public void mergeSample(XGMSample sample, XGM xgm, HashSet<XGMFMCommand> updatedCommands)
    {
        // find if already exist
        XGMSample matchingSample = findMatchingSample(sample);

        // we already have the sample ?
        if (matchingSample != null)
        {
            bool sameDuration = J.Round(sample.getLength() / 60) == J.Round(matchingSample.getLength() / 60);
            // update VGM so it now uses the matching sample id
            xgm.updateSampleCommands(sample.id, matchingSample.id, sameDuration ? -1L : (sample.getLength() * 44100L) / XGMSample.XGM_FULL_RATE,
                    updatedCommands);

            mergedSample++;
            if (Launcher.verbose)
                Console.WriteLine("Found duplicated sample #" + sample.id + " (merged)");
        }
        else
        {
            // maximum number of sample reached ?
            if (sharedSamples.Count >= (249 - 1))
            {
                Console.Error.WriteLine("Warning: multi tracks XGM is limited to 248 samples max, some samples will be lost !");
                return;
            }

            // just add the sample
            sharedSamples.Add(sample);

            int newId = sharedSamples.Count;
            // update VGM so it now uses the new sample id
            xgm.updateSampleCommands(sample.id, newId, -1L, updatedCommands);
            // update sample id
            sample.id = newId;
        }
    }

    // consider packed flag and align size on 256 bytes
    public int getTotalPSGMusicDataSize()
    {
        int result = 0;

        if (packed)
        {
            foreach (XGM xgm in xgms)
                result += Util.align(xgm.getPackedPSGMusicDataSize(), 256);
        }
        else
        {
            foreach (XGM xgm in xgms)
                result += Util.align(xgm.getPSGMusicDataSize(), 256);
        }

        return result;
    }

    // consider packed flag and align size on 256 bytes
    public int getTotalFMMusicDataSize()
    {
        int result = 0;

        if (packed)
        {
            foreach (XGM xgm in xgms)
                result += Util.align(xgm.getPackedFMMusicDataSize(), 256);
        }
        else
        {
            foreach (XGM xgm in xgms)
                result += Util.align(xgm.getFMMusicDataSize(), 256);
        }

        return result;
    }

    public int getPCMDataSize()
    {
        int result = 0;

        foreach (XGMSample sample in sharedSamples)
            result += sample.data.Length;

        return result;
    }

    public byte[] getPCMDataArray()
    {
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        for (int s = 0; s < sharedSamples.Count; s++)
        {
            byte[] copy = (byte[])sharedSamples[s].data.Clone();

            // sign the sample
            for (int i = 0; i < copy.Length; i++)
                copy[i] += 0x80;

            result.write(copy);
        }

        return result.toByteArray();
    }

    public byte[] asByteArray()
    {
        int offset;
        int data;
        int len;
        ByteArrayOutputStream result = new ByteArrayOutputStream();

        // 0000: XGM2 (ignored when compiled in ROM)
        if (!packed)
            result.write(Util.getBytes("XGM2"));

        // 0004: version (0x10 currently)
        result.write(0x10);

        // 0005: format description (see xgm2.txt)
        data = 0;
        // bit #0: NTSC / PAL information: 0=NTSC 1=PAL
        if (pal)
            data |= 1;
        // bit #1: multi tracks file: 0=No 1=Yes (always 1 here)
        data |= 2;
        // bit #2: GD3 tags: 0=No 1=Yes
        if (hasGD3)
            data |= 4;
        // bit #3: packed FM / PSG / GD3 data blocks: 0=No 1=Yes
        if (packed)
            data |= 8;
        // write format
        result.write(data);

        // 0006-0007: SLEN = Sample data bloc size / 256 (ex: $0200 means 512*256 = 131072 bytes)
        data = Util.align(getPCMDataSize(), 256) >> 8;
        result.write(data >> 0);
        result.write(data >> 8);
        // 0008-0009: FMLEN = FM music data block size / 256 (ex: $0040 means 64*256 = 16384 bytes)
        data = getTotalFMMusicDataSize() >> 8;
        result.write(data >> 0);
        result.write(data >> 8);
        // 000A-000B: PSGLEN = PSG music data block size / 256 (ex: $0020 means 32*256 = 8192 bytes)
        data = getTotalPSGMusicDataSize() >> 8;
        result.write(data >> 0);
        result.write(data >> 8);

        // 000C-0203: SID (sample id) table
        // size = 512-8 = 504 bytes so end of table will align on 256 bytes in ROM
        offset = 0;
        for (int s = 0; s < sharedSamples.Count; s++)
        {
            XGMSample sample = sharedSamples[s];
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
        for (int s = sharedSamples.Count + 1; s < (504 / 2); s++)
        {
            result.write(0xFF);
            result.write(0xFF);
        }

        // 0204-0303: FMID (FM track id) table (multi-tracks)
        // contain address for FM music data tracks (fixed size = 256 bytes = 128 entries)
        offset = 0;
        foreach (XGM xgm in xgms)
        {
            // each entry of the table consist of 2 bytes for address (FM data track size is aligned to 256 bytes)
            // entry+$0: FM data track address / 256
            result.write(offset >> 0);
            result.write(offset >> 8);

            // next track (align on 256 bytes)
            offset += Util.align(packed ? xgm.getPackedFMMusicDataSize() : xgm.getFMMusicDataSize(), 256) >> 8;
        }
        for (int i = xgms.Count; i < 128; i++)
        {
            result.write(0xFF);
            result.write(0xFF);
        }

        // 0304-0403: PSGID (PSG track id) table (multi-tracks)
        // contain address for PSG music data tracks (fixed size = 256 bytes = 128 entries)
        offset = 0;
        foreach (XGM xgm in xgms)
        {
            // each entry of the table consist of 2 bytes for address (PSG data track size is aligned to 256 bytes)
            // entry+$0: PSG data track address / 256
            result.write(offset >> 0);
            result.write(offset >> 8);

            // next track (align on 256 bytes)
            offset += Util.align(packed ? xgm.getPackedPSGMusicDataSize() : xgm.getPSGMusicDataSize(), 256) >> 8;
        }
        for (int i = xgms.Count; i < 128; i++)
        {
            result.write(0xFF);
            result.write(0xFF);
        }

        // 0404-xx03: sample data (see SLEN field for size)
        result.write(getPCMDataArray());

        if (packed)
        {
            // xx04-xx04: FM music data (all tracks, 256 bytes padded)
            foreach (XGM xgm in xgms)
                result.write(Util.align(xgm.getPackedFMMusicDataArray(), 256, 0));
            // xx04-xx04: PSG music data (all tracks, 256 bytes padded)
            foreach (XGM xgm in xgms)
                result.write(Util.align(xgm.getPackedPSGMusicDataArray(), 256, 0));
        }
        else
        {
            // xx04-xx04: FM music data (all tracks, 256 bytes padded)
            foreach (XGM xgm in xgms)
                result.write(Util.align(xgm.getFMMusicDataArray(), 256, 0));
            // xx04-xx04: PSG music data (all tracks, 256 bytes padded)
            foreach (XGM xgm in xgms)
                result.write(Util.align(xgm.getPSGMusicDataArray(), 256, 0));
        }

        // xx04-xx04: GD3/XD3
        if (hasGD3)
        {
            // 0x04-0x03: GID (GD3/XD3 tags id) table (multi-tracks)
            // contain address for GD3/XD3 tags tracks (fixed size = 256 bytes = 128 entries)
            offset = 0;
            foreach (XGM xgm in xgms)
            {
                // each entry of the table consist of 2 bytes for address as we consider we will never require more than
                // 65536 bytes for GD3 tags
                // an entry with -1 (0xFFFF) mean that we don't have GD3 tags for that track.
                // all GD3 tags with address >=65536 are ignored (set to -1)
                // entry+$0: GD3 tags data track address
                if (packed)
                {
                    if (xgm.xd3 != null)
                    {
                        result.write(offset >> 0);
                        result.write(offset >> 8);
                        // next track
                        offset += xgm.xd3.getTotalDataSize();
                    }
                }
                else if (xgm.gd3 != null)
                {
                    result.write(offset >> 0);
                    result.write(offset >> 8);
                    // next track
                    offset += xgm.gd3.getTotalDataSize();
                }
                else
                {
                    // no tag here
                    result.write(0xFF);
                    result.write(0xFF);
                }
            }
            for (int i = xgms.Count; i < 128; i++)
            {
                result.write(0xFF);
                result.write(0xFF);
            }

            // xx04-xx04: GD3/XD3 tags data
            foreach (XGM xgm in xgms)
            {
                if (packed)
                {
                    if (xgm.xd3 != null)
                        result.write(xgm.gd3.asByteArray());
                }
                else if (xgm.gd3 != null)
                    result.write(xgm.gd3.asByteArray());
            }
        }

        return result.toByteArray();
    }
}
