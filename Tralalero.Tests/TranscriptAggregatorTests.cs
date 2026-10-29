using Tralalero.Core.Transcription;

namespace Tralalero.Tests;

public sealed class TranscriptAggregatorTests
{
    [Fact]
    public void Append_JoinsPhrasesWithASingleSpace()
    {
        var aggregator = new TranscriptAggregator();

        aggregator.Append("Hello");
        aggregator.Append("world.");

        Assert.Equal("Hello world.", aggregator.Text);
        Assert.Equal(2, aggregator.SegmentCount);
    }

    [Fact]
    public void Append_DoesNotAddSpaceBeforePunctuation()
    {
        var aggregator = new TranscriptAggregator();

        aggregator.Append("Hello");
        aggregator.Append(".");

        Assert.Equal("Hello.", aggregator.Text);
    }

    [Fact]
    public void Append_NormalizesWhitespace()
    {
        var aggregator = new TranscriptAggregator();

        aggregator.Append("  Hello \t  world  ");
        aggregator.Append(" again ");

        Assert.Equal("Hello world again", aggregator.Text);
    }

    [Fact]
    public void Append_IgnoresEmptyPhrases()
    {
        var aggregator = new TranscriptAggregator();

        aggregator.Append("   ");
        aggregator.Append(null);

        Assert.Equal(string.Empty, aggregator.Text);
        Assert.Equal(0, aggregator.SegmentCount);
    }

    [Fact]
    public void PromptTail_ReturnsOnlyTheLastCharacters()
    {
        var aggregator = new TranscriptAggregator();
        aggregator.Append("uno dos tres cuatro cinco");

        var tail = aggregator.PromptTail(10);

        Assert.Equal("atro cinco", tail);
    }

    [Fact]
    public void Reset_ClearsTextAndCounter()
    {
        var aggregator = new TranscriptAggregator();
        aggregator.Append("hola");

        aggregator.Reset();

        Assert.Equal(string.Empty, aggregator.Text);
        Assert.Equal(0, aggregator.SegmentCount);
    }
}
