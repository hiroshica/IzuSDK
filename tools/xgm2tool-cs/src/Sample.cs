using System.Text;
namespace Isdk.Xgm2Tool;


/**
 * Sample (signed 8 bits data)
 * 
 * @author Stephane Dallongeville
 */
public class Sample
{
    public byte[] data;
    public int sampleRate;

    public Sample(byte[] data, int sampleRate)
    {
        this.data = data;
        this.sampleRate = sampleRate;
    }

    public int getSample(int index)
    {
        return data[index];
    }

    public int getSize()
    {
        return data.Length;
    }

    public bool match(Sample sample, int startIndex)
    {
        if (startIndex >= data.Length)
            return false;

        byte[] sdata = sample.data;

        for (int i = startIndex, si = 0; (i < data.Length) && (si < sdata.Length); i++, si++)
            if (data[i] != sdata[si])
                return false;

        return true;
    }

    public bool isValid()
    {
        // 0 value in sampleRate means sample in invalid
        return sampleRate != 0;
    }
}
