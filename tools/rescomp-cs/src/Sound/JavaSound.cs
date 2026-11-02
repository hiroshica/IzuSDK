namespace Sgdk.Rescomp.Sound;

/// <summary>
/// rescomp.jar が WAV を読むときに使う Java Sound (javax.sound.sampled) の変換を、同じ結果になるように移したもの。
/// WaveFileReader → AudioFloatConverter → チャンネルのミックス → SoftLinearResampler2 → 8 ビット符号付き。
/// 区切りの大きさ (64KB ずつ読む) まで結果に効くので、呼び出しの形もそのまま再現している。
/// </summary>
public static class JavaSound
{
    /// <summary>Java 版 SoundUtil.getRawDataFromWAV(path, 8, rate, mono=true, signed=true, bigEndian=false)。</summary>
    public static byte[] GetRawDataFromWav(string path, int rate)
    {
        var src = WavSource.Open(path);
        FloatStream stream = new DirectFloatStream(src);

        int adjRate = rate <= 0 ? (int)src.SampleRate : rate;
        if (src.Channels != 1)
            stream = new ChannelMixer(stream, 1);

        long frameLength = stream.FrameLength;
        bool resample = Math.Abs((float)adjRate - stream.SampleRate) > 0.000001;
        if (resample)
        {
            stream = new Resampler(stream, adjRate);
            frameLength = -1;
        }

        // AudioInputStream (1 フレーム = 1 バイト) + BufferedInputStream + NetworkUtil.download の読み方
        var result = new MemoryStream();
        var data = new byte[64 * 1024];
        var fbuf = new float[0];
        long framePos = 0;
        while (true)
        {
            int len = data.Length;
            if (frameLength != -1)
            {
                if (framePos >= frameLength) break;
                if (len > frameLength - framePos) len = (int)(frameLength - framePos);
            }

            if (fbuf.Length < len) fbuf = new float[len];
            int ret = stream.Read(fbuf, 0, len);
            if (ret < 0) break;
            for (int i = 0; i < ret; i++)
            {
                float x = fbuf[i];
                data[i] = (byte)F2I(x > 0 ? x * 127 : x * 128);
            }
            framePos += ret;
            result.Write(data, 0, ret);
            // 再サンプリングしないときは、途中で短く返っても続けて読むだけなので、結果は変わらない
        }
        return result.ToArray();
    }

    /// <summary>Java の float → int (範囲外は飽和、NaN は 0)。</summary>
    internal static int F2I(float f)
    {
        if (float.IsNaN(f)) return 0;
        if (f >= 2147483647f) return int.MaxValue;
        if (f <= -2147483648f) return int.MinValue;
        return (int)f;
    }
}

/// <summary>WaveFileReader が読む PCM の WAV。</summary>
sealed class WavSource
{
    public int Channels;
    public float SampleRate;
    public int Bits;
    public bool Unsigned;
    public int FrameSize;
    public long FrameLength;
    byte[] file;
    int pos;
    int end;
    long framePos;

    public static WavSource Open(string path)
    {
        var w = new WavSource { file = File.ReadAllBytes(path) };
        w.Parse(path);
        return w;
    }

    int ReadIntBE()
    {
        if (pos + 4 > file.Length) throw new EndOfStreamException();
        int v = (file[pos] << 24) | (file[pos + 1] << 16) | (file[pos + 2] << 8) | file[pos + 3];
        pos += 4;
        return v;
    }

    int ReadIntLE()
    {
        if (pos + 4 > file.Length) throw new EndOfStreamException();
        int v = file[pos] | (file[pos + 1] << 8) | (file[pos + 2] << 16) | (file[pos + 3] << 24);
        pos += 4;
        return v;
    }

    short ReadShortLE()
    {
        if (pos + 2 > file.Length) throw new EndOfStreamException();
        short v = (short)(file[pos] | (file[pos + 1] << 8));
        pos += 2;
        return v;
    }

    void Skip(int n) => pos = Math.Min(file.Length, pos + Math.Max(0, n));

    void Parse(string path)
    {
        try
        {
            if (ReadIntBE() != 0x52494646) Unsupported(path, "not a WAVE file");
            ReadIntLE();
            if (ReadIntBE() != 0x57415645) Unsupported(path, "not a WAVE file");

            while (true)
            {
                int fmt = ReadIntBE();
                if (fmt == 0x666d7420) break;
                int length = ReadIntLE();
                if (length % 2 > 0) length++;
                Skip(length);
            }

            int chunkLen = ReadIntLE();
            int chunkStart = pos;
            int wavType = ReadShortLE();
            // PCM 以外 (float、拡張形式、A-law、μ-law) は jar に任せる
            if (wavType != 1)
                throw new NotPortedException("WAV format " + wavType + " is not supported yet by the C# rescomp: '" + path + "'");

            Channels = ReadShortLE();
            if (Channels <= 0) Unsupported(path, "Invalid number of channels");
            SampleRate = (float)(ReadIntLE() & 0xFFFFFFFFL);
            ReadIntLE();
            ReadShortLE();
            Bits = ReadShortLE();
            if (Bits <= 0) Unsupported(path, "Invalid bitsPerSample");
            if (Bits % 8 != 0 || Bits > 32)
                throw new NotPortedException(Bits + " bits WAV is not supported yet by the C# rescomp: '" + path + "'");
            Unsigned = Bits == 8;
            if (chunkStart + chunkLen > pos) Skip(chunkStart + chunkLen - pos);

            while (true)
            {
                int hdr = ReadIntBE();
                if (hdr == 0x64617461) break;
                int length = ReadIntLE();
                if (length % 2 > 0) length++;
                Skip(length);
            }
            long dataLength = ReadIntLE() & 0xFFFFFFFFL;
            FrameSize = ((Bits + 7) / 8) * Channels;
            FrameLength = dataLength / FrameSize;
            end = file.Length;
        }
        catch (EndOfStreamException)
        {
            Unsupported(path, "Not a valid WAV file");
        }
    }

    static void Unsupported(string path, string msg) =>
        throw new IOException("Error while converting WAV '" + path + "' to PCM format (" + msg + ")");

    /// <summary>AudioInputStream.read (フレーム数で打ち切り、端数のバイトは次に回す)。ここでは端数は捨ててよい。</summary>
    public int Read(byte[] b, int len)
    {
        len -= len % FrameSize;
        if (len == 0) return 0;
        if (framePos >= FrameLength) return -1;
        if (len / FrameSize > FrameLength - framePos) len = (int)(FrameLength - framePos) * FrameSize;
        int avail = end - pos;
        if (avail <= 0) return -1;
        int n = Math.Min(len, avail);
        n -= n % FrameSize;
        if (n == 0) return -1;
        Array.Copy(file, pos, b, 0, n);
        pos += n;
        framePos += n / FrameSize;
        return n;
    }
}

abstract class FloatStream
{
    public abstract int Channels { get; }
    public abstract float SampleRate { get; }
    public abstract long FrameLength { get; }
    public abstract int Read(float[] b, int off, int len);
    public int Read(float[] b) => Read(b, 0, b.Length);
}

/// <summary>AudioFloatInputStream.DirectAudioFloatInputStream と AudioFloatConverter (8U / 16SL / 24SL / 32SL)。</summary>
sealed class DirectFloatStream : FloatStream
{
    readonly WavSource src;
    readonly int framesizePc;
    byte[] buffer = new byte[0];

    public DirectFloatStream(WavSource src)
    {
        this.src = src;
        framesizePc = src.FrameSize / src.Channels;
    }

    public override int Channels => src.Channels;
    public override float SampleRate => src.SampleRate;
    public override long FrameLength => src.FrameLength;

    public override int Read(float[] b, int off, int len)
    {
        int bLen = len * framesizePc;
        if (buffer.Length < bLen) buffer = new byte[bLen];
        int ret = src.Read(buffer, bLen);
        if (ret == -1) return -1;
        int n = ret / framesizePc;
        int ix = 0;
        for (int i = 0; i < n; i++)
        {
            float v;
            switch (src.Bits)
            {
                case 8:
                {
                    sbyte x = (sbyte)(buffer[ix++] - 128);
                    v = x > 0 ? x / 127.0f : x / 128.0f;
                    break;
                }
                case 16:
                {
                    short x = (short)(buffer[ix] | (buffer[ix + 1] << 8));
                    ix += 2;
                    v = x > 0 ? x / 32767.0f : x / 32768.0f;
                    break;
                }
                case 24:
                {
                    int x = buffer[ix] | (buffer[ix + 1] << 8) | (buffer[ix + 2] << 16);
                    ix += 3;
                    if (x > 0x7FFFFF) x -= 0x1000000;
                    v = x > 0 ? x / 8388607.0f : x / 8388608.0f;
                    break;
                }
                default:
                {
                    int x = buffer[ix] | (buffer[ix + 1] << 8) | (buffer[ix + 2] << 16) | (buffer[ix + 3] << 24);
                    ix += 4;
                    v = x * (1.0f / (float)0x7FFFFFFF);
                    break;
                }
            }
            b[off + i] = v;
        }
        return n;
    }
}

/// <summary>AudioFloatFormatConverter.AudioFloatInputStreamChannelMixer (ここでは mono にまとめるときだけ)。</summary>
sealed class ChannelMixer : FloatStream
{
    readonly FloatStream ais;
    readonly int sourceChannels;
    readonly int targetChannels;
    float[] conv = new float[0];

    public ChannelMixer(FloatStream ais, int targetChannels)
    {
        this.ais = ais;
        sourceChannels = ais.Channels;
        this.targetChannels = targetChannels;
    }

    public override int Channels => targetChannels;
    public override float SampleRate => ais.SampleRate;
    public override long FrameLength => ais.FrameLength;

    public override int Read(float[] b, int off, int len)
    {
        int len2 = (len / targetChannels) * sourceChannels;
        if (conv.Length < len2) conv = new float[len2];
        int ret = ais.Read(conv, 0, len2);
        if (ret < 0) return ret;
        int cs = sourceChannels;
        for (int i = 0, ix = off; i < len2; i += cs, ix++)
            b[ix] = conv[i];
        for (int c = 1; c < sourceChannels; c++)
            for (int i = c, ix = off; i < len2; i += cs, ix++)
                b[ix] += conv[i];
        float vol = 1f / ((float)sourceChannels);
        for (int i = 0, ix = off; i < len2; i += cs, ix++)
            b[ix] *= vol;
        return (ret / sourceChannels) * targetChannels;
    }
}

/// <summary>AudioFloatFormatConverter.AudioFloatInputStreamResampler + SoftLinearResampler2。</summary>
sealed class Resampler : FloatStream
{
    const int BufferLen = 512;
    const int Pad = 2;
    const int Pad2 = Pad * 2;

    readonly FloatStream ais;
    readonly float targetRate;
    readonly int nrofchannels;
    readonly float[] pitch = new float[1];
    readonly float[] ibuffer2;
    readonly float[][] ibuffer;
    float ibufferIndex;
    int ibufferLen;
    float[][] cbuffer;
    readonly float[] ix = new float[1];
    readonly int[] ox = new int[1];

    public Resampler(FloatStream ais, float targetRate)
    {
        this.ais = ais;
        this.targetRate = targetRate;
        nrofchannels = ais.Channels;
        pitch[0] = ais.SampleRate / targetRate;
        ibuffer = new float[nrofchannels][];
        for (int c = 0; c < nrofchannels; c++) ibuffer[c] = new float[BufferLen + Pad2];
        ibuffer2 = new float[nrofchannels * BufferLen];
        ibufferIndex = BufferLen + Pad;
        ibufferLen = BufferLen;
    }

    public override int Channels => nrofchannels;
    public override float SampleRate => targetRate;
    public override long FrameLength => -1;

    void ReadNextBuffer()
    {
        if (ibufferLen == -1) return;
        for (int c = 0; c < nrofchannels; c++)
        {
            var buff = ibuffer[c];
            int bufferLenPad = ibufferLen + Pad2;
            for (int i = ibufferLen, x = 0; i < bufferLenPad; i++, x++)
                buff[x] = buff[i];
        }
        ibufferIndex -= ibufferLen;
        ibufferLen = ais.Read(ibuffer2);
        if (ibufferLen >= 0)
        {
            while (ibufferLen < ibuffer2.Length)
            {
                int ret = ais.Read(ibuffer2, ibufferLen, ibuffer2.Length - ibufferLen);
                if (ret == -1) break;
                ibufferLen += ret;
            }
            Array.Fill(ibuffer2, 0f, ibufferLen, ibuffer2.Length - ibufferLen);
            ibufferLen /= nrofchannels;
        }
        else
            Array.Fill(ibuffer2, 0f);

        int ibuffer2Len = ibuffer2.Length;
        for (int c = 0; c < nrofchannels; c++)
        {
            var buff = ibuffer[c];
            for (int i = c, x = Pad2; i < ibuffer2Len; i += nrofchannels, x++)
                buff[x] = ibuffer2[i];
        }
    }

    public override int Read(float[] b, int off, int len)
    {
        if (cbuffer == null || cbuffer[0].Length < len / nrofchannels)
        {
            cbuffer = new float[nrofchannels][];
            for (int c = 0; c < nrofchannels; c++) cbuffer[c] = new float[len / nrofchannels];
        }
        if (ibufferLen == -1) return -1;
        if (len < 0) return 0;
        int offlen = off + len;
        int remain = len / nrofchannels;
        int destPos = 0;
        int inEnd = ibufferLen;
        while (remain > 0)
        {
            if (ibufferLen >= 0)
            {
                if (ibufferIndex >= (ibufferLen + Pad)) ReadNextBuffer();
                inEnd = ibufferLen + Pad;
            }
            if (ibufferLen < 0)
            {
                inEnd = Pad2;
                if (ibufferIndex >= inEnd) break;
            }
            if (ibufferIndex < 0) break;
            int preDestPos = destPos;
            for (int c = 0; c < nrofchannels; c++)
            {
                ix[0] = ibufferIndex;
                ox[0] = destPos;
                Interpolate(ibuffer[c], ix, inEnd, pitch, cbuffer[c], ox, len / nrofchannels);
            }
            ibufferIndex = ix[0];
            destPos = ox[0];
            remain -= destPos - preDestPos;
        }
        for (int c = 0; c < nrofchannels; c++)
        {
            int x = 0;
            var buff = cbuffer[c];
            for (int i = c + off; i < offlen; i += nrofchannels)
                b[i] = buff[x++];
        }
        return len - remain * nrofchannels;
    }

    /// <summary>SoftLinearResampler2.interpolate (pitchstep = 0 のとき)。</summary>
    static void Interpolate(float[] input, float[] inOffset, float inEnd, float[] startpitch, float[] output, int[] outOffset, int outEnd)
    {
        float pitch = startpitch[0];
        float ix = inOffset[0];
        int ox = outOffset[0];
        float ixEnd = inEnd;
        int oxEnd = outEnd;
        if (!(ix < ixEnd && ox < oxEnd)) return;
        int pIx = JavaSound.F2I(ix * (1 << 15));
        int pIxEnd = JavaSound.F2I(ixEnd * (1 << 15));
        int pPitch = JavaSound.F2I(pitch * (1 << 15));
        if (pPitch == 0) pPitch = 1;
        pitch = pPitch * (1f / (1 << 15));

        int pIxLen = pIxEnd - pIx;
        int pMod = pIxLen % pPitch;
        if (pMod != 0) pIxLen += pPitch - pMod;
        int oxEnd2 = ox + pIxLen / pPitch;
        if (oxEnd2 < oxEnd) oxEnd = oxEnd2;
        while (ox < oxEnd)
        {
            int iix = pIx >> 15;
            float fix = ix - iix;
            float i = input[iix];
            output[ox++] = i + (input[iix + 1] - i) * fix;
            pIx += pPitch;
            ix += pitch;
        }

        inOffset[0] = ix;
        outOffset[0] = ox;
        startpitch[0] = pitch;
    }
}
