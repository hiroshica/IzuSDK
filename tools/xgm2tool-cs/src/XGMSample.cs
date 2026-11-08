using System.Text;
namespace Isdk.Xgm2Tool;





/**
 * XGM Sample have fixed sample rate of 13.3 or 6.65 Khz.<br>
 * Size and Offset are 64 bytes boundary.
 * 
 * @author Stephane
 */
public class XGMSample
{
    public const int XGM_FULL_RATE = 13300;
    public const int XGM_HALF_RATE = (XGM_FULL_RATE / 2);

    public int id;
    public byte[] data;

    // used only for VGM to XGM conversion
    public bool halfRate;
    public int originId;
    public int originAddr;

    public XGMSample(int id, byte[] data, bool halfRate, int originId, int originAddr)
    {
        this.id = id;
        this.data = data;

        // only used for VGM to XGM conversion
        this.halfRate = halfRate;
        this.originId = originId;
        this.originAddr = originAddr;
    }

    public XGMSample(int id, byte[] data) : this(id, data, false, -1, 0)
    {
    }

    public XGMSample(int id, byte[] data, int offset, int len) : this(id, J.CopyOfRange(data, offset, offset + len))
    {
    }

    public static XGMSample createFromVGMSample(int id, SampleBank.InternalSample sample)
    {
        bool half = (sample.rate < 10000);

        return new XGMSample(id, Util.resample(sample.getBank().data, sample.addr, sample.len, sample.rate, half ? XGM_HALF_RATE : XGM_FULL_RATE, 256), half,
                sample.id, sample.addr);
    }

    public int getLength()
    {
        return data.Length;
    }

    public double getSimilarityScore(XGMSample originSample)
    {
        int deltaSize = originSample.data.Length - data.Length;
        // origin sample is longer ? cannot use a shorter sample to replace it...
        if (deltaSize > 150)
            return 0d;

        int minSize = Math.Min(data.Length, originSample.data.Length);

        // simple sample compare (exact match)
        return J.ArrayEquals(J.CopyOf(data, minSize), J.CopyOf(originSample.data, minSize)) ? 1d : 0d;
    }
}
