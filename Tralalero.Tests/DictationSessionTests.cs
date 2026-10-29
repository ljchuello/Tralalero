using Tralalero.Core.Audio;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.Settings;
using Tralalero.Core.Transcription;

namespace Tralalero.Tests;

public sealed class DictationSessionTests
{
    [Fact]
    public async Task StartAndStop_TranscribesAPhraseAndCompletes()
    {
        var recorder = new FakeRecorder();
        var transcriber = new FakeTranscriber();
        var session = new DictationSession(() => recorder, transcriber, NullAppLogger.Instance);
        var completed = new TaskCompletionSource<DictationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.Completed += (_, args) => completed.TrySetResult(args);

        await session.StartAsync(0, new AppSettings { MaxRecordingMinutes = 1 });
        recorder.Emit(Join(Silence(500), Tone(1000), Silence(1800)));
        await transcriber.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.StopAsync();
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(string.IsNullOrEmpty(result.Error?.Message));
        Assert.Equal("hola mundo", result.Transcript);
        Assert.Equal(1, result.SegmentCount);
        Assert.Equal(DictationState.Idle, session.State);
        session.Dispose();
    }

    [Fact]
    public async Task StartAsync_RejectsASecondSession()
    {
        var recorder = new FakeRecorder();
        var session = new DictationSession(() => recorder, new FakeTranscriber(), NullAppLogger.Instance);
        await session.StartAsync(0, new AppSettings());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.StartAsync(0, new AppSettings()));

        session.Dispose();
    }

    private static byte[] Join(params byte[][] chunks)
    {
        var result = new byte[chunks.Sum(chunk => chunk.Length)];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    private static byte[] Silence(int milliseconds) => new byte[16000 * 2 * milliseconds / 1000];

    private static byte[] Tone(int milliseconds, double amplitude = 0.3)
    {
        var samples = 16000 * milliseconds / 1000;
        var buffer = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 220 * i / 16000) * short.MaxValue * amplitude);
            BitConverter.TryWriteBytes(buffer.AsSpan(i * 2, 2), value);
        }

        return buffer;
    }

    private sealed class FakeRecorder : IAudioRecorder
    {
        public event EventHandler<AudioChunkEventArgs>? AudioAvailable;
        public event EventHandler<float>? LevelChanged;

        public bool IsRecording { get; private set; }

        public void Start(int deviceNumber)
        {
            IsRecording = true;
            LevelChanged?.Invoke(this, 0.2f);
        }

        public Task StopAsync()
        {
            IsRecording = false;
            return Task.CompletedTask;
        }

        public void Emit(byte[] pcm) => AudioAvailable?.Invoke(this, new AudioChunkEventArgs(pcm));

        public void Dispose()
        {
        }
    }

    private sealed class FakeTranscriber : ITranscriber
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> TranscribeAsync(
            AudioSegment segment,
            TranscriptionOptions options,
            string? prompt,
            CancellationToken cancellationToken)
        {
            Called.TrySetResult();
            return Task.FromResult("hola mundo");
        }
    }
}
