using System.Text;
namespace Sgdk.Xgm2Tool;


/**
 * Single byte of VGM sample data (time is expressed in 1/44100 of second)
 * 
 * @author steph
 */
public class VGMSampleData : IComparable<VGMSampleData>
{
    public long time;
    public byte data;

    public VGMSampleData(long time, byte data)
    {
        this.time = time;
        this.data = data;
    }
    public int CompareTo(VGMSampleData sample)
    {
        return J.Compare(time, sample.time);
    }
}
