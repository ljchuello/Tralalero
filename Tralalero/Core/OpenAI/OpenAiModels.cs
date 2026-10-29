namespace Tralalero.Core.OpenAI;

/// <summary>Current OpenAI model and useful suggestions for compatible endpoints.</summary>
public static class OpenAiModels
{
    public const string Default = "gpt-transcribe";

    public static IReadOnlyList<ModelOption> Suggestions { get; } = new[]
    {
        new ModelOption(
            Default,
            Default,
            "OpenAI's recommended file-transcription model."),
        new ModelOption(
            "whisper-1",
            "whisper-1",
            "Common model name exposed by OpenAI-compatible servers."),
        new ModelOption(
            "whisper-large-v3",
            "whisper-large-v3",
            "faster-whisper / Groq-style model identifier."),
        new ModelOption(
            "whisper-large-v3-turbo",
            "whisper-large-v3-turbo",
            "Fast large-v3 variant on some compatible servers."),
        new ModelOption(
            "openai/whisper-1",
            "openai/whisper-1",
            "Provider-namespaced Whisper model, for example on OpenRouter."),
        new ModelOption(
            "openai/whisper-large-v3",
            "openai/whisper-large-v3",
            "Provider-namespaced Whisper large-v3, for example on OpenRouter."),
        new ModelOption(
            "Systran/faster-whisper-large-v3",
            "Systran/faster-whisper-large-v3",
            "Local faster-whisper server identifier; not an OpenRouter model.")
    };
}

/// <summary>A display-friendly model entry.</summary>
public sealed record ModelOption(string Id, string DisplayName, string Description)
{
    public override string ToString() => DisplayName;
}
