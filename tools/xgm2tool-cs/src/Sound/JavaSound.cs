namespace Isdk.Xgm2Tool.Sound;

/// <summary>
/// xgm2tool が PCM のサンプリング周波数を変えるときに使う Java Sound (javax.sound.sampled) の変換を、同じ結果になるように移したもの。
/// 8 ビット符号なし・モノラルの入力に限る。AudioFloatConverter(8U) → SoftLinearResampler2 → AudioFloatConverter(8U)。
/// (rescomp-cs の WAV 変換と同じ仕組み)
/// </summary>
public static class JavaSound
{
    internal static int F2I(float f)
    {
        if (float.IsNaN(f)) return 0;
        if (f >= 2147483647f) return int.MaxValue;
        if (f <= -2147483648f) return int.MinValue;
        return (int)f;
    }

    /// <summary>Util.resample(data, offset, len, inputRate, outputRate, 1) の変換部分。</summary>
    public static byte[] ResampleU8Mono(byte[] data, int offset, int len, int inputRate, int outputRate)
    {
        // AudioSystem.getAudioInputStream は、形式が同じなら元のストリームをそのまま返す
        if (Math.Abs((float)outputRate - (float)inputRate) <= 0.000001)
        {
            var copy = new byte[len];
            Array.Copy(data, offset, copy, 0, len);
            return copy;
        }

        FloatStream stream = new Resampler(new U8Source(data, offset, len, inputRate), outputRate);
        var result = new MemoryStream();
        var buf = new byte[8 * 1024];
        var fbuf = new float[buf.Length];
        while (true)
        {
            int ret = stream.Read(fbuf, 0, buf.Length);
            if (ret < 0) break;
            for (int i = 0; i < ret; i++)
            {
                float x = fbuf[i];
                buf[i] = (byte)F2I(128 + (x > 0 ? x * 127 : x * 128));
            }
            result.Write(buf, 0, ret);
        }
        return result.ToArray();
    }
}

/// <summary>AudioInputStream(ByteArrayInputStream) + DirectAudioFloatInputStream + AudioFloatConversion8U。</summary>
sealed class U8Source : FloatStream
{
    readonly byte[] data;
    int pos;
    readonly int end;
    readonly float rate;
    readonly long frameLength;
    long framePos;

    public U8Source(byte[] data, int offset, int len, int rate)
    {
        this.data = data;
        pos = offset;
        end = offset + len;
        this.rate = rate;
        frameLength = len;
    }

    public override int Channels => 1;
    public override float SampleRate => rate;
    public override long FrameLength => frameLength;

    public override int Read(float[] b, int off, int len)
    {
        // AudioInputStream.read: フレーム数で打ち切る
        if (framePos >= frameLength) return -1;
        if (len > frameLength - framePos) len = (int)(frameLength - framePos);
        int avail = end - pos;
        if (avail <= 0) return -1;
        int n = Math.Min(len, avail);
        for (int i = 0; i < n; i++)
        {
            sbyte x = (sbyte)(data[pos++] - 128);
            b[off + i] = x > 0 ? x / 127.0f : x / 128.0f;
        }
        framePos += n;
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
