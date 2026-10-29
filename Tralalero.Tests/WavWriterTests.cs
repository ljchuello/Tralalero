using System.Text;
using Tralalero.Core.Audio;

namespace Tralalero.Tests;

public sealed class WavWriterTests
{
    [Fact]
    public void CreateWav_WritesValidPcmHeaderAndPayload()
    {
        var pcm = new byte[3200];

        var wav = WavWriter.CreateWav(pcm);

        Assert.Equal(44 + pcm.Length, wav.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));

        var sampleRate = BitConverter.ToInt32(wav, 24);
        var dataSize = BitConverter.ToInt32(wav, 40);
        Assert.Equal(16000, sampleRate);
        Assert.Equal(pcm.Length, dataSize);
    }
}
