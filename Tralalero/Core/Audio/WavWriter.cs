using System.Text;

namespace Tralalero.Core.Audio;

/// <summary>Creates a standard PCM WAV container around 16-bit audio.</summary>
public static class WavWriter
{
    public static byte[] CreateWav(byte[] pcm16, int sampleRate = 16000, short channels = 1)
    {
        ArgumentNullException.ThrowIfNull(pcm16);
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        const short bitsPerSample = 16;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var byteRate = sampleRate * blockAlign;

        using var stream = new MemoryStream(44 + pcm16.Length);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcm16.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcm16.Length);
            writer.Write(pcm16);
        }

        return stream.ToArray();
    }
}
