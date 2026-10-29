namespace Tralalero.Core.OpenAI;

/// <summary>Current file-transcription models supported by Tralalero.</summary>
public static class OpenAiModels
{
    public const string Default = "gpt-transcribe";

    public static IReadOnlyList<ModelOption> Known { get; } = new[]
    {
        new ModelOption(
            Default,
            "gpt-transcribe",
            "Recommended by OpenAI for accurate, current file transcription.")
    };
}

/// <summary>A display-friendly model entry.</summary>
public sealed record ModelOption(string Id, string DisplayName, string Description)
{
    public override string ToString() => DisplayName;
}
