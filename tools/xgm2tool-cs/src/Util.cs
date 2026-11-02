using System.Text;

namespace Sgdk.Xgm2Tool;

public static class Util
{
    public static int align(int value, int align) => ((value + (align - 1)) / align) * align;

    public static void align(ByteArrayOutputStream outS, int align, int fill)
    {
        int size = outS.size();
        int len = Util.align(size, align) - size;
        for (int i = 0; i < len; i++)
            outS.write(fill);
    }

    public static byte[] align(byte[] data, int align, int fill)
    {
        var result = new ByteArrayOutputStream(data.Length);
        result.write(data, 0, data.Length);
        Util.align(result, align, fill);
        return result.toByteArray();
    }

    public static int getASCIIStringSize(byte[] inb, int offset)
    {
        int off = offset;
        int result = 0;
        while (getInt8(inb, off) != 0)
        {
            result++;
            off++;
        }
        return result;
    }

    public static int getWideStringSize(byte[] inb, int offset)
    {
        int off = offset;
        int result = 0;
        while (getInt16(inb, off) != 0)
        {
            result++;
            off += 2;
        }
        return result;
    }

    public static string getWideString(byte[] inb, int offset) => getWideString(inb, offset, getWideStringSize(inb, offset));
    public static string getASCIIString(byte[] inb, int offset) => getASCIIString(inb, offset, getASCIIStringSize(inb, offset));
    public static string getWideString(byte[] inb, int offset, int length) => Encoding.Unicode.GetString(inb, offset, length * 2);
    // Java の US_ASCII は 0x80 以上を '?' (U+FFFD) にするが、C# の ASCII も '?' にする
    public static string getASCIIString(byte[] inb, int offset, int length) => Encoding.ASCII.GetString(inb, offset, length);
    public static string getUTF8String(byte[] inb, int offset, int length) => Encoding.UTF8.GetString(inb, offset, length);
    public static int getStringSize(string text) => text.Length;

    public static byte[] getBytes(string text, bool utf16) => utf16 ? Encoding.Unicode.GetBytes(text) : Encoding.ASCII.GetBytes(text);
    public static byte[] getBytes(string text) => getBytes(text, false);

    public static bool arrayEquals(byte[] array1, byte[] array2, int size)
    {
        for (int i = 0; i < size; i++)
            if (array1[i] != array2[i])
                return false;
        return true;
    }

    public static int swapNibbles(byte value)
    {
        // Java の byte は符号付きなので、右シフトは符号を伸ばす
        int v = (sbyte)value;
        return ((v >> 4) & 0xF) | ((v << 4) & 0xF0);
    }

    public static string getBytesAsHexaString(byte[] data)
    {
        if (data == null) return "null";
        return getBytesAsHexaString(data, 0, data.Length, 64);
    }

    public static string getBytesAsHexaString(byte[] data, int offset, int len, int maxlen)
    {
        if (data == null) return "null";
        var result = new StringBuilder();
        for (int i = 0; i < Math.Min(maxlen, len); i++)
        {
            int v = data[offset + i] & 0xFF;
            if (v < 16) result.Append('0');
            result.Append(v.ToString("X"));
        }
        if (len > maxlen) result.Append("..");
        return result.ToString();
    }

    public static string getHexaString(int value, int minSize)
    {
        var result = new StringBuilder();
        for (int i = 7; i >= 0; i--)
        {
            int v = (value >> (i * 4)) & 0xF;
            if (v != 0 || i < minSize)
                result.Append(v.ToString("X"));
        }
        return result.ToString();
    }

    public static int getInt8(byte[] data, int offset) => data[offset] & 0xFF;

    public static int getInt16(byte[] data, int offset) => (data[offset] & 0xFF) + ((data[offset + 1] & 0xFF) << 8);

    public static int getInt24(byte[] data, int offset) =>
        (data[offset] & 0xFF) + ((data[offset + 1] & 0xFF) << 8) + ((data[offset + 2] & 0xFF) << 16);

    public static int getInt32(byte[] data, int offset)
    {
        long res = (data[offset] & 0xFF);
        res += (data[offset + 1] & 0xFF) << 8;
        res += (data[offset + 2] & 0xFF) << 16;
        res += (data[offset + 3] & 0xFF) << 24;
        return (int)res;
    }

    public static void setInt32(byte[] array, int offset, int value)
    {
        array[offset + 0] = (byte)((value >> 0) & 0xFF);
        array[offset + 1] = (byte)((value >> 8) & 0xFF);
        array[offset + 2] = (byte)((value >> 16) & 0xFF);
        array[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    public static void setInt24(byte[] array, int offset, int value)
    {
        array[offset + 0] = (byte)((value >> 0) & 0xFF);
        array[offset + 1] = (byte)((value >> 8) & 0xFF);
        array[offset + 2] = (byte)((value >> 16) & 0xFF);
    }

    public static void setInt16(byte[] array, int offset, int value)
    {
        array[offset + 0] = (byte)((value >> 0) & 0xFF);
        array[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    public static void setInt8(byte[] array, int offset, int value) => array[offset] = (byte)((value >> 0) & 0xFF);

    public static byte[] readBinaryFile(string fileName) => File.Exists(fileName) ? File.ReadAllBytes(fileName) : null;

    public static void writeBinaryFile(byte[] data, string fileName) => File.WriteAllBytes(fileName, data);

    /// <summary>8 ビット符号なし・モノラルの PCM を Java Sound と同じやり方で別の周波数にする。</summary>
    public static byte[] resample(byte[] data, int offset, int len, int inputRate, int outputRate, int align)
    {
        var result = Sound.JavaSound.ResampleU8Mono(data, offset, len, inputRate, outputRate);
        var ms = new MemoryStream();
        ms.Write(result, 0, result.Length);

        if (align > 1)
        {
            int mask = align - 1;
            int size = align - ((int)ms.Length & mask);
            if (size != align)
                for (int i = 0; i < size; i++)
                    ms.WriteByte(0x80);
        }
        return ms.ToArray();
    }

    /// <summary>VGM の時間 (1/44100 秒) を mm:ss.SSS にする。</summary>
    public static string formatVGMTime(long vgmTime)
    {
        long ms = vgmTime * 1000L / 44100L;
        return ((ms / 60000) % 60).ToString("00") + ":" + ((ms / 1000) % 60).ToString("00") + "." + (ms % 1000).ToString("000");
    }

    public static bool isDiffRate(int rate1, int rate2)
    {
        if (rate1 == rate2) return false;
        int r1 = Math.Max(100, rate1);
        int r2 = Math.Max(100, rate2);
        double f = (double)r1 / (double)r2;
        return Math.Abs(100 - (f * 100d)) > 10d;
    }
}
