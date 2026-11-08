using Isdk.Rescomp.Resources;

namespace Isdk.Rescomp.Processors;

public sealed class WavProcessor : IProcessor
{
    public string Id => "WAV";

    public Resource Execute(string[] fields)
    {
        if (fields.Length < 4)
        {
            Console.WriteLine("Wrong WAV definition");
            Console.WriteLine("WAV name \"file\" driver [out_rate [far]]");
            Console.WriteLine("  name      variable name");
            Console.WriteLine("  file      path of the .wav file (will be automatically converted int the correct format)");
            Console.WriteLine("  driver    specify the Z80 driver we will use to play the WAV file:");
            Console.WriteLine("              PCM / DEFAULT (0 value is not anymore accepted)");
            Console.WriteLine("                Single PCM channel sound driver.");
            Console.WriteLine("                It can play a sample (8 bit signed) from 8 Khz up to 32 Khz rate.");
            Console.WriteLine("                Method to use: SND_PCM_startPlay(..)");
            Console.WriteLine("              2DPCM (2ADPCM and 1 are not anymore accepted)");
            Console.WriteLine("                2 PCM channels DPCM sound driver.");
            Console.WriteLine("                It can mix up to 2 (4 bit) DCPM samples at a fixed 22050 Hz rate.");
            Console.WriteLine("                Method to use: SND_DPCM2_startPlay(..)");
            Console.WriteLine("              4PCM (2 and 3 are not anymore accepted)");
            Console.WriteLine("                4 PCM channels sample sound driver with volume support.");
            Console.WriteLine("                It can mix up to 4 samples (8 bit signed) at a fixed 16 Khz rate with 16 levels of volume.");
            Console.WriteLine("                Method to use: SND_PCM4_startPlay(..)");
            Console.WriteLine("  out_rate  output PCM rate, this parameter is meaningful only for PCM driver.");
            Console.WriteLine("              PCM driver accepts following values: 8000, 11025, 13400, 16000, 22050, 32000 (default is 16000 if omitted)");
            Console.WriteLine("  far       'far' binary data flag to put it at the end of the ROM (useful for bank switch, default = FALSE)");
            return null;
        }

        string id = fields[1];
        string fileIn = JavaCompat.AdjustPath(Compiler.ResDir, fields[2]);
        var driver = Util.GetSoundDriver(fields[3]);

        int outRate = driver switch
        {
            SoundDriver.PCM => 16000,
            SoundDriver.DPCM2 => 22050,
            SoundDriver.PCM4 => 16000,
            _ => 0,
        };
        if (fields.Length >= 5)
            outRate = JavaCompat.ParseInt(fields[4], outRate);
        bool far = false;
        if (fields.Length >= 6)
            far = JavaCompat.ParseBoolean(fields[5], far);

        byte[] pcmData = Sound.JavaSound.GetRawDataFromWav(fileIn, outRate);

        if (driver == SoundDriver.DPCM2)
            pcmData = Util.DpcmPack(pcmData);

        Compiler.AddResourceFile(fileIn);

        bool dpcm = driver == SoundDriver.DPCM2;
        return new Bin(id, pcmData, dpcm ? 128 : 256, dpcm ? 128 : 256, dpcm ? 136 : 0, Compression.NONE, far);
    }
}
