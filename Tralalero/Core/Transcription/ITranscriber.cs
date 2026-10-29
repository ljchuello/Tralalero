using Tralalero.Core.Audio;

namespace Tralalero.Core.Transcription;

/// <summary>Options that affect a single transcription request.</summary>
public sealed record TranscriptionOptions(string Model, string Language);

/// <summary>A WAV segment ready to be sent to a transcription provider.</summary>
public sealed record AudioSegment(byte[] WavData, TimeSpan Duration, DateTimeOffset RecordedAt);

/// <summary>Provider seam. A local Whisper implementation can be added later.</summary>
public interface ITranscriber
{
    Task<string> TranscribeAsync(
        AudioSegment segment,
        TranscriptionOptions options,
        string? prompt,
        CancellationToken cancellationToken);
}
