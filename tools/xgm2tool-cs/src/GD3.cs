using System.Text;
namespace Isdk.Xgm2Tool;



public class GD3
{
    public const string XGMTOOL_PRINT = "Optimized with XGMTool";

    public int version;

    public string trackName_EN;
    public string trackName_JP;
    public string gameName_EN;
    public string gameName_JP;
    public string systemName_EN;
    public string systemName_JP;
    public string authorName_EN;
    public string authorName_JP;
    public string date;
    public string vgmConversionAuthor;
    public string notes;

    public GD3()
    {
        trackName_EN = "";
        trackName_JP = "";
        gameName_EN = "";
        gameName_JP = "";
        systemName_EN = "";
        systemName_JP = "";
        authorName_EN = "";
        authorName_JP = "";
        date = "";
        vgmConversionAuthor = "";
        notes = "";

        version = 0x100;
    }

    public GD3(XD3 xd3)
    {
        trackName_EN = xd3.trackName;
        trackName_JP = xd3.trackName;
        gameName_EN = xd3.gameName;
        gameName_JP = xd3.gameName;
        systemName_EN = "SEGA Mega Drive";
        systemName_JP = "SEGA Mega Drive";
        authorName_EN = xd3.authorName;
        authorName_JP = xd3.authorName;
        date = xd3.date;
        vgmConversionAuthor = xd3.conversionAuthor;
        notes = xd3.notes;

        version = 0x100;
    }

    public GD3(byte[] data, int baseOffset)
    {
        int offset = baseOffset;

        if (!Launcher.silent)
            Console.WriteLine("Parsing GD3...\n");

        string id = Util.getASCIIString(data, offset, 4);

        if (!StringUtil.Equals(id, "Gd3 "))
            Console.WriteLine("Error: GD3 header not recognized !\n");
        offset += 4;

        // get version info
        version = Util.getInt32(data, offset);
        offset += 4;

        // get total size for GD3 data (we can ignore this field)
        Util.getInt32(data, offset);
        offset += 4;

        trackName_EN = Util.getWideString(data, offset);
        offset += (trackName_EN.Length + 1) * 2;
        trackName_JP = Util.getWideString(data, offset);
        offset += (trackName_JP.Length + 1) * 2;
        gameName_EN = Util.getWideString(data, offset);
        offset += (gameName_EN.Length + 1) * 2;
        gameName_JP = Util.getWideString(data, offset);
        offset += (gameName_JP.Length + 1) * 2;
        systemName_EN = Util.getWideString(data, offset);
        offset += (systemName_EN.Length + 1) * 2;
        systemName_JP = Util.getWideString(data, offset);
        offset += (systemName_JP.Length + 1) * 2;
        authorName_EN = Util.getWideString(data, offset);
        offset += (authorName_EN.Length + 1) * 2;
        authorName_JP = Util.getWideString(data, offset);
        offset += (authorName_JP.Length + 1) * 2;
        date = Util.getWideString(data, offset);
        offset += (date.Length + 1) * 2;
        vgmConversionAuthor = Util.getWideString(data, offset);
        offset += (vgmConversionAuthor.Length + 1) * 2;
        notes = Util.getWideString(data, offset);
        offset += (notes.Length + 1) * 2;

        // not ending with our signature ?
        if (!vgmConversionAuthor.EndsWith(XGMTOOL_PRINT))
        {
            if (!StringUtil.isEmpty(vgmConversionAuthor))
                vgmConversionAuthor += " - ";

            // add XGMTool signature
            vgmConversionAuthor += XGMTOOL_PRINT;
        }
    }

    public int computeDataSize()
    {
        return (trackName_EN.Length * 2) + (trackName_JP.Length * 2) + (gameName_EN.Length * 2)
                + (gameName_JP.Length * 2) + (systemName_EN.Length * 2) + (systemName_JP.Length * 2)
                + (authorName_EN.Length * 2) + (authorName_JP.Length * 2) + (date.Length * 2)
                + (vgmConversionAuthor.Length * 2) + (notes.Length * 2) + (11 * 2);
    }

    public int getTotalDataSize()
    {
        return computeDataSize() + 12;
    }

    public byte[] asByteArray()
    {
        // align size on 2 bytes
        int size = computeDataSize();
        byte[] result = new byte[size + 12];
        int offset = 0;

        // header
        Array.Copy(Util.getBytes("Gd3 "), 0, result, offset, 4);
        offset += 4;
        // version
        Util.setInt32(result, offset, version);
        offset += 4;
        // size
        Util.setInt32(result, offset, size);
        offset += 4;

        // fields
        size = trackName_EN.Length * 2;
        Array.Copy(Util.getBytes(trackName_EN, true), 0, result, offset, size);
        // +2 for 0 ending character in C
        offset += size + 2;
        size = trackName_JP.Length * 2;
        Array.Copy(Util.getBytes(trackName_JP, true), 0, result, offset, size);
        offset += size + 2;
        size = gameName_EN.Length * 2;
        Array.Copy(Util.getBytes(gameName_EN, true), 0, result, offset, size);
        offset += size + 2;
        size = gameName_JP.Length * 2;
        Array.Copy(Util.getBytes(gameName_JP, true), 0, result, offset, size);
        offset += size + 2;
        size = systemName_EN.Length * 2;
        Array.Copy(Util.getBytes(systemName_EN, true), 0, result, offset, size);
        offset += size + 2;
        size = systemName_JP.Length * 2;
        Array.Copy(Util.getBytes(systemName_JP, true), 0, result, offset, size);
        offset += size + 2;
        size = authorName_EN.Length * 2;
        Array.Copy(Util.getBytes(authorName_EN, true), 0, result, offset, size);
        offset += size + 2;
        size = authorName_JP.Length * 2;
        Array.Copy(Util.getBytes(authorName_JP, true), 0, result, offset, size);
        offset += size + 2;
        size = date.Length * 2;
        Array.Copy(Util.getBytes(date, true), 0, result, offset, size);
        offset += size + 2;
        size = vgmConversionAuthor.Length * 2;
        Array.Copy(Util.getBytes(vgmConversionAuthor, true), 0, result, offset, size);
        offset += size + 2;
        size = notes.Length * 2;
        Array.Copy(Util.getBytes(notes, true), 0, result, offset, size);

        return result;
    }
}
