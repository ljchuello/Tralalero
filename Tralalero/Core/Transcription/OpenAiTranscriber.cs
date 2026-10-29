using Tralalero.Core.Audio;
using Tralalero.Core.OpenAI;

namespace Tralalero.Core.Transcription;

/// <summary>OpenAI implementation of <see cref="ITranscriber"/>.</summary>
public sealed class OpenAiTranscriber : ITranscriber
{
    private readonly OpenAiClient _client;

    public OpenAiTranscriber(OpenAiClient client)
    {
        _client = client;
    }

    public async Task<string> TranscribeAsync(
        AudioSegment segment,
        TranscriptionOptions options,
        string? prompt,
        CancellationToken cancellationToken)
    {
        var response = await _client
            .TranscribeAsync(segment.WavData, options.Model, options.Language, prompt, cancellationToken)
            .ConfigureAwait(false);
        return response.Text;
    }
}
