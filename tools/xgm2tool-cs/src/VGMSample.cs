using System.Text;
namespace Isdk.Xgm2Tool;

/**
 * 
 */


/**
 * VGM Sample (time is always expressed in 1/44100 of second)
 * 
 * @author Stephane Dallongeville
 */
public class VGMSample
{
    public class VGMGapCounter
    {
        public int count;

        public VGMGapCounter()
        {
            count = 1;
        }

        public void inc()
        {
            count++;
        }
    }

    public List<VGMSampleData> sampleDataList;

    public VGMSample()
    {
        sampleDataList = new();
    }

    public void addSampleData(int offset, byte data)
    {
        sampleDataList.Add(new VGMSampleData(offset, data));
    }

    public void sort()
    {
        // sort samples on time
        J.Sort(sampleDataList);
    }

    public int getMeanSampleRate()
    {
        double sumDelta = 0d;

        for (int i = 0; i < (sampleDataList.Count - 1); i++)
        {
            VGMSampleData s0 = sampleDataList[i + 0];
            VGMSampleData s1 = sampleDataList[i + 1];

            sumDelta += s1.time - s0.time;
        }

        if (sampleDataList.Count > 1)
            sumDelta /= (sampleDataList.Count - 1);

        return (int) J.Round(44100d / sumDelta);
    }

    public int getWantedSampleRate()
    {
        Dictionary<long, VGMGapCounter> gapHisto = new();
        List<long> gapKeys = new();

        for (int i = 0; i < (sampleDataList.Count - 1); i++)
        {
            VGMSampleData s0 = sampleDataList[i + 0];
            VGMSampleData s1 = sampleDataList[i + 1];

            // key is time gap
            long key = (long)(s1.time - s0.time);
            // get number of occurrence for this gap
            // create new count for this gap
            if (!gapHisto.TryGetValue(key, out VGMGapCounter count))
            {
                gapHisto[key] = new VGMGapCounter();
                gapKeys.Add(key);
            }
            // just increment it
            else
                count.inc();
        }

        // find max count
        int maxCount = 0;
        foreach (VGMGapCounter gap in gapHisto.Values)
        {
            int cnt = gap.count;

            if (cnt > maxCount)
                maxCount = cnt;
        }

        // allowed gap minimum count
        int minimumGapCount = maxCount / 3;

        double countedGap = 0d;
        double timeSum = 0d;
        // Java の HashMap と同じ順番で足す (double の足し算は順番で結果が変わる)
        foreach (long gapKey in J.HashOrder(gapKeys, J.HashOfLong))
        {
            KeyValuePair<long, VGMGapCounter> entry = new(gapKey, gapHisto[gapKey]);
            int cnt = entry.Value.count;

            // enough count ?
            if (cnt >= minimumGapCount)
            {
                // time sum
                timeSum += (double) entry.Key * (double) cnt;
                // counted gap
                countedGap += cnt;
            }
        }

        // return wanted sample rate (estimation)
        return (int) J.Round(timeSum / countedGap);
    }
}
