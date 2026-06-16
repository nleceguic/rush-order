using System.Media;

namespace RushOrder.Desktop.Helpers;

internal static class NotificationSound
{
    private static readonly byte[]? _wavNewOrder = BuildBeepWav(880,  180);
    private static readonly byte[]? _wavUrgent   = BuildDoubleBeepWav(1400, 80, 1100, 100);

    public static void PlayNewOrder()  => PlayWav(_wavNewOrder);
    public static void PlayUrgent()    => PlayWav(_wavUrgent);
    public static void PlayAlert()     => SystemSounds.Asterisk.Play();

    private static void PlayWav(byte[]? wav)
    {
        try
        {
            if (wav is not null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        using var ms     = new MemoryStream(wav);
                        using var player = new SoundPlayer(ms);
                        player.PlaySync();
                    }
                    catch { }
                });
            }
            else { SystemSounds.Exclamation.Play(); }
        }
        catch { }
    }

    private static byte[]? BuildDoubleBeepWav(int freq1, int dur1Ms, int freq2, int dur2Ms)
    {
        try
        {
            var b1 = BuildPcmSamples(freq1, dur1Ms);
            var gap = new short[44100 * 60 / 1000];  // 60ms silence
            var b2  = BuildPcmSamples(freq2, dur2Ms);
            var all = b1.Concat(gap).Concat(b2).ToArray();
            return WrapInWavHeader(all);
        }
        catch { return null; }
    }

    private static short[] BuildPcmSamples(int frequency, int durationMs)
    {
        const int rate = 44100;
        int n = rate * durationMs / 1000;
        var s = new short[n];
        for (int i = 0; i < n; i++)
        {
            var t    = (double)i / rate;
            var fade = i < 441 ? (double)i / 441 : i > n - 441 ? (double)(n - i) / 441 : 1.0;
            s[i] = (short)(Math.Sin(2 * Math.PI * frequency * t) * fade * 28000);
        }
        return s;
    }

    private static byte[] WrapInWavHeader(short[] samples)
    {
        const int  rate          = 44100;
        const short bits         = 16;
        const short ch           = 1;
        int         dataSize     = samples.Length * 2;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); bw.Write((short)1); bw.Write(ch); bw.Write(rate);
        bw.Write(rate * ch * bits / 8); bw.Write((short)(ch * bits / 8)); bw.Write(bits);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        foreach (var s in samples) bw.Write(s);
        return ms.ToArray();
    }

    private static byte[]? BuildBeepWav(int frequency, int durationMs)
    {
        try { return WrapInWavHeader(BuildPcmSamples(frequency, durationMs)); }
        catch { return null; }
    }
}
