using Tralalero.Core.OpenAI;

namespace Tralalero.Tests;

public sealed class OpenAiEndpointTests
{
    [Theory]
    [InlineData("https://api.openai.com/v1/", "https://api.openai.com/v1")]
    [InlineData("  http://localhost:1234/v1  ", "http://localhost:1234/v1")]
    [InlineData("", "https://api.openai.com/v1")]
    public void NormalizeBaseUrl_TrimsAndDefaults(string input, string expected)
    {
        Assert.Equal(expected, OpenAiEndpoint.NormalizeBaseUrl(input));
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", "https://api.openai.com/v1/audio/transcriptions")]
    [InlineData("http://localhost:1234/v1/", "http://localhost:1234/v1/audio/transcriptions")]
    [InlineData("http://localhost:1234/v1/audio/transcriptions", "http://localhost:1234/v1/audio/transcriptions")]
    public void BuildTranscriptionUrl_AvoidsDuplicatePath(string input, string expected)
    {
        Assert.Equal(expected, OpenAiEndpoint.BuildTranscriptionUrl(input));
    }

    [Fact]
    public void BuildModelsUrl_RemovesTranscriptionPath()
    {
        Assert.Equal(
            "http://localhost:1234/v1/models",
            OpenAiEndpoint.BuildModelsUrl("http://localhost:1234/v1/audio/transcriptions"));
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", true)]
    [InlineData("https://api.openai.com/v1/", true)]
    [InlineData("http://localhost:1234/v1", false)]
    [InlineData("https://example.test/v1", false)]
    public void IsOpenAi_DetectsOnlyOpenAiHost(string url, bool expected)
    {
        Assert.Equal(expected, OpenAiEndpoint.IsOpenAi(url));
    }

    [Theory]
    [InlineData("http://localhost:1234/v1", true)]
    [InlineData("http://127.0.0.1:8080/v1", true)]
    [InlineData("http://192.168.1.10:8080/v1", true)]
    [InlineData("https://example.test/v1", true)]
    [InlineData("http://example.test/v1", false)]
    public void TryValidate_AllowsLocalHttpAndRequiresHttpsForPublicHosts(string url, bool expected)
    {
        Assert.Equal(expected, OpenAiEndpoint.TryValidate(url, out _, out _));
    }

    [Fact]
    public void GetHost_IncludesNonDefaultPort()
    {
        Assert.Equal("localhost:1234", OpenAiEndpoint.GetHost("http://localhost:1234/v1"));
        Assert.Equal("api.openai.com", OpenAiEndpoint.GetHost("https://api.openai.com/v1"));
    }
}
