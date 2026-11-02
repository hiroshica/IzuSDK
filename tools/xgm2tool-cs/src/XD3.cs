using System.Text;
namespace Sgdk.Xgm2Tool;



public class XD3
{
    public string trackName;
    public string gameName;
    public string authorName;
    public string date;
    public string conversionAuthor;
    public string notes;
    public int duration; // duration in frame
    public int loopDuration; // duration in frame (0 if no loop)

    public XD3()
    {
        trackName = "";
        gameName = "";
        authorName = "";
        date = "";
        conversionAuthor = "";
        notes = "";
        duration = 0;
        loopDuration = 0;
    }

    public XD3(byte[] data, int baseOffset)
    {
        int offset = baseOffset;

        if (!Launcher.silent)
            Console.WriteLine("Parsing XD3...\n");

        // total size for XD3 data (we can ignore this field)
        Util.getInt32(data, offset);
        offset += 4;

        // fields
        trackName = Util.getASCIIString(data, offset);
        // +1 for 0 ending character
        offset += trackName.Length + 1;
        gameName = Util.getASCIIString(data, offset);
        // +1 for 0 ending character
        offset += gameName.Length + 1;
        authorName = Util.getASCIIString(data, offset);
        // +1 for 0 ending character
        offset += authorName.Length + 1;
        date = Util.getASCIIString(data, offset);
        // +1 for 0 ending character
        offset += date.Length + 1;
        conversionAuthor = Util.getASCIIString(data, offset);
        // +1 for 0 ending character
        offset += conversionAuthor.Length + 1;
        notes = Util.getASCIIString(data, offset);
        // +1 for 0 ending character
        offset += notes.Length + 1;

        // duration and loop
        duration = Util.getInt32(data, offset + 0);
        loopDuration = Util.getInt32(data, offset + 4);
    }

    public XD3(GD3 gd3, int duration, int loopDuration)
    {
        trackName = gd3.trackName_EN;
        gameName = gd3.gameName_EN;
        authorName = gd3.authorName_EN;
        date = gd3.date;
        conversionAuthor = gd3.vgmConversionAuthor;
        notes = gd3.notes;

        this.duration = duration;
        this.loopDuration = loopDuration;
    }

    public int computeDataSize()
    {
        return trackName.Length + gameName.Length + authorName.Length + date.Length + conversionAuthor.Length
                + notes.Length + (6 * 1) + 8; // +8 for durations informations
    }

    public int getTotalDataSize()
    {
        return computeDataSize() + 4;
    }

    public byte[] asByteArray()
    {
        // align size on 2 bytes
        int size = (computeDataSize() + 1) & unchecked((int)0xFFFFFFFE);
        byte[] result = new byte[size + 4];
        int offset = 0;

        // size of XD3 infos
        Util.setInt32(result, offset, size);
        offset += 4;

        // fields
        size = trackName.Length;
        Array.Copy(Util.getBytes(trackName), 0, result, offset, size);
        // +1 for 0 ending character in C
        offset += size + 1;
        size = gameName.Length;
        Array.Copy(Util.getBytes(gameName), 0, result, offset, size);
        // +1 for 0 ending character in C
        offset += size + 1;
        size = authorName.Length;
        Array.Copy(Util.getBytes(authorName), 0, result, offset, size);
        // +1 for 0 ending character in C
        offset += size + 1;
        size = date.Length;
        Array.Copy(Util.getBytes(date), 0, result, offset, size);
        // +1 for 0 ending character in C
        offset += size + 1;
        size = conversionAuthor.Length;
        Array.Copy(Util.getBytes(conversionAuthor), 0, result, offset, size);
        // +1 for 0 ending character in C
        offset += size + 1;
        size = notes.Length;
        Array.Copy(Util.getBytes(notes), 0, result, offset, size);
        // +1 for 0 ending character in C
        offset += size + 1;

        // duration and loop
        Util.setInt32(result, offset + 0, duration);
        Util.setInt32(result, offset + 4, loopDuration);

        return result;
    }
}
