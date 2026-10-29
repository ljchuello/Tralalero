using System.Text;
using Tralalero.Core.Audio;

namespace Tralalero.Tests;

public sealed class VoiceActivitySegmenterTests
{
    private const int SampleRate = 16000;

    [Fact]
    public void Push_EmitsOneSegmentForAShortPhrase()
    {
        var segmenter = new VoiceActivitySegmenter(VadSensitivity.Medium);
        var pcm = Join(Silence(500), Tone(1000), Silence(2000));

        var segments = segmenter.Push(pcm);

        var segment = Assert.Single(segments);
        Assert.True(segment.Duration > TimeSpan.FromSeconds(0.9));
        Assert.Equal("RIFF", Encoding.ASCII.GetString(segment.WavData, 0, 4));
    }

    [Fact]
    public void Push_DoesNotEmitSegmentsForSilence()
    {
        var segmenter = new VoiceActivitySegmenter(VadSensitivity.Medium);

        var segments = segmenter.Push(Silence(2500));

        Assert.Empty(segments);
        Assert.Empty(segmenter.Flush());
    }

    [Fact]
    public void Flush_EmitsThePhraseStillInProgress()
    {
        var segmenter = new VoiceActivitySegmenter(VadSensitivity.Medium);
        segmenter.Push(Join(Silence(500), Tone(800)));

        var segments = segmenter.Flush();

        var segment = Assert.Single(segments);
        Assert.True(segment.Duration > TimeSpan.FromSeconds(0.5));
    }

    private static byte[] Join(params byte[][] chunks)
    {
        var length = chunks.Sum(chunk => chunk.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    private static byte[] Silence(int milliseconds) =>
        new byte[SampleRate * 2 * milliseconds / 1000];

    private static byte[] Tone(int milliseconds, double amplitude = 0.3)
    {
        var samples = SampleRate * milliseconds / 1000;
        var buffer = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 220 * i / SampleRate) * short.MaxValue * amplitude);
            BitConverter.TryWriteBytes(buffer.AsSpan(i * 2, 2), value);
        }

        return buffer;
    }
}
