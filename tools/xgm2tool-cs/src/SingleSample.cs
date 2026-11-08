using System.Text;
namespace Isdk.Xgm2Tool;


/**
 * Single sample class (time is always expressed in 1/44100 of second)
 * 
 * @author Stephane Dallongeville
 */
public class SingleSample : IComparable<SingleSample>
{
    public const int BASE_RATE = 44100;

    bool newSample;
    int sample;
    long time;

    public SingleSample(int sample, long time, bool newSample)
    {
        this.newSample = newSample;
        this.sample = sample;
        this.time = time;
    }

    public SingleSample(int sample, long time) : this(sample, time, false)
    {
    }
    public int CompareTo(SingleSample ss)
    {
        return J.Compare(time, ss.time);
    }
}
