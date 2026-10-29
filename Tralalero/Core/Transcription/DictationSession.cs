using System.Threading.Channels;
using Tralalero.Core.Audio;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.Settings;

namespace Tralalero.Core.Transcription;

public enum DictationState
{
    Idle,
    Listening,
    Finalizing,
    Error
}

public sealed class TranscriptUpdatedEventArgs : EventArgs
{
    public required string Transcript { get; init; }
    public required int SegmentCount { get; init; }
    public required TimeSpan Elapsed { get; init; }
}

public sealed class DictationStateChangedEventArgs : EventArgs
{
    public required DictationState State { get; init; }
    public string? Message { get; init; }
}

public sealed class DictationErrorEventArgs : EventArgs
{
    public required Exception Error { get; init; }
}

public sealed class DictationCompletedEventArgs : EventArgs
{
    public required string Transcript { get; init; }
    public required TimeSpan Duration { get; init; }
    public required int SegmentCount { get; init; }
    public Exception? Error { get; init; }
}

/// <summary>
/// Owns one dictation session: microphone -> VAD -> sequential transcription
/// queue -> aggregated transcript. One session is created per recording.
/// </summary>
public sealed class DictationSession : IDisposable
{
    private readonly Func<IAudioRecorder> _recorderFactory;
    private readonly ITranscriber _transcriber;
    private readonly IAppLogger _logger;
    private readonly Channel<AudioSegment> _segments = Channel.CreateUnbounded<AudioSegment>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly CancellationTokenSource _cancellation = new();
    private readonly TranscriptAggregator _aggregator = new();
    private readonly object _gate = new();

    private IAudioRecorder? _recorder;
    private Task? _recorderStopTask;
    private VoiceActivitySegmenter? _segmenter;
    private AppSettings? _settings;
    private Task? _processingTask;
    private Task? _finalizationTask;
    private int _state = (int)DictationState.Idle;
    private int _segmentCount;
    private DateTimeOffset _startedAt;
    private DateTimeOffset? _stoppedAt;
    private Exception? _error;
    private int _maxRecordingSeconds;
    private bool _disposed;

    public DictationSession(
        Func<IAudioRecorder> recorderFactory,
        ITranscriber transcriber,
        IAppLogger logger)
    {
        _recorderFactory = recorderFactory;
        _transcriber = transcriber;
        _logger = logger;
    }

    public DictationState State => (DictationState)Volatile.Read(ref _state);

    public string Transcript => _aggregator.Text;

    public TimeSpan Elapsed => (_stoppedAt ?? DateTimeOffset.UtcNow) - _startedAt;

    public event EventHandler<TranscriptUpdatedEventArgs>? TranscriptUpdated;

    public event EventHandler<DictationStateChangedEventArgs>? StateChanged;

    public event EventHandler<DictationErrorEventArgs>? Error;

    public event EventHandler<DictationCompletedEventArgs>? Completed;

    public event EventHandler<float>? LevelChanged;

    public Task StartAsync(int deviceNumber, AppSettings settings, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        if (State != DictationState.Idle)
        {
            throw new InvalidOperationException("A dictation session is already running.");
        }

        _settings = settings.Clone();
        _startedAt = DateTimeOffset.UtcNow;
        _maxRecordingSeconds = Math.Max(60, _settings.MaxRecordingMinutes * 60);
        _segmenter = new VoiceActivitySegmenter(_settings.VadSensitivity);

        _recorder = _recorderFactory();
        _recorder.AudioAvailable += OnAudioAvailable;
        _recorder.LevelChanged += OnLevelChanged;

        _processingTask = ProcessSegmentsAsync(_cancellation.Token);

        try
        {
            _recorder.Start(deviceNumber);
            SetState(DictationState.Listening, "Listening");
        }
        catch
        {
            _cancellation.Cancel();
            _segments.Writer.TryComplete();
            _recorder.AudioAvailable -= OnAudioAvailable;
            _recorder.LevelChanged -= OnLevelChanged;
            _recorder.Dispose();
            _recorder = null;
            SetState(DictationState.Error, "Microphone error");
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>Stops the microphone, flushes the last phrase, and waits for pending requests.</summary>
    public Task StopAsync()
    {
        lock (_gate)
        {
            _finalizationTask ??= FinalizeAsync();
            return _finalizationTask;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _segments.Writer.TryComplete();

        try
        {
            _recorder?.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort shutdown.
        }

        _recorder?.Dispose();
        _cancellation.Dispose();
    }

    private Task StopRecorderAsync()
    {
        lock (_gate)
        {
            if (_recorder is null)
            {
                return Task.CompletedTask;
            }

            // The error path and finalization may race; both must await the same stop.
            return _recorderStopTask ??= _recorder.StopAsync();
        }
    }

    private async Task ProcessSegmentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var segment in _segments.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var options = new TranscriptionOptions(_settings!.Model, _settings.Language);
                var prompt = _aggregator.PromptTail();
                var text = await _transcriber
                    .TranscribeAsync(segment, options, prompt, cancellationToken)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                _aggregator.Append(text);
                Interlocked.Increment(ref _segmentCount);
                RaiseTranscriptUpdated();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during disposal or cancellation.
        }
        catch (Exception ex)
        {
            _error = ex;
            _logger.Error("A transcription request failed.", ex);
            SetState(DictationState.Error, "Transcription error");
            RaiseError(ex);

            try
            {
                await StopRecorderAsync().ConfigureAwait(false);
            }
            catch (Exception stopException)
            {
                _logger.Warn("Could not stop the microphone after a transcription error.", stopException);
            }

            _segments.Writer.TryComplete();
        }
    }

    private async Task FinalizeAsync()
    {
        try
        {
            if (State == DictationState.Idle)
            {
                return;
            }

            if (State != DictationState.Error)
            {
                SetState(DictationState.Finalizing, "Finishing");
            }

            _stoppedAt ??= DateTimeOffset.UtcNow;

            try
            {
                await StopRecorderAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warn("The microphone did not stop cleanly.", ex);
            }

            if (State != DictationState.Error && _segmenter is not null)
            {
                foreach (var segment in _segmenter.Flush())
                {
                    _segments.Writer.TryWrite(segment);
                }
            }

            _segments.Writer.TryComplete();

            if (_processingTask is not null)
            {
                try
                {
                    await _processingTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _error ??= ex;
                    _logger.Error("The transcription queue ended with an error.", ex);
                }
            }

            if (_error is null)
            {
                SetState(DictationState.Idle, "Ready");
            }
            else
            {
                SetState(DictationState.Error, "Transcription error");
            }

            RaiseCompleted(new DictationCompletedEventArgs
            {
                Transcript = _aggregator.Text,
                Duration = Elapsed,
                SegmentCount = _segmentCount,
                Error = _error
            });
        }
        catch (Exception ex)
        {
            _error ??= ex;
            _logger.Error("The dictation session could not be finalized.", ex);
            SetState(DictationState.Error, "Unexpected error");
            RaiseError(ex);
        }
    }

    private void OnAudioAvailable(object? sender, AudioChunkEventArgs e)
    {
        if (State != DictationState.Listening || _segmenter is null)
        {
            return;
        }

        foreach (var segment in _segmenter.Push(e.Buffer))
        {
            _segments.Writer.TryWrite(segment);
        }

        if (Elapsed >= TimeSpan.FromSeconds(_maxRecordingSeconds))
        {
            _logger.Info("Maximum recording duration reached; stopping automatically.");
            _ = Task.Run(StopAsync);
        }
    }

    private void OnLevelChanged(object? sender, float level)
    {
        if (State == DictationState.Listening)
        {
            RaiseLevel(level);
        }
    }

    private void RaiseTranscriptUpdated()
    {
        Raise(TranscriptUpdated, new TranscriptUpdatedEventArgs
        {
            Transcript = _aggregator.Text,
            SegmentCount = _segmentCount,
            Elapsed = Elapsed
        });
    }

    private void SetState(DictationState state, string? message)
    {
        Volatile.Write(ref _state, (int)state);
        Raise(StateChanged, new DictationStateChangedEventArgs { State = state, Message = message });
    }

    private void RaiseError(Exception exception) =>
        Raise(Error, new DictationErrorEventArgs { Error = exception });

    private void RaiseCompleted(DictationCompletedEventArgs args) => Raise(Completed, args);

    private void RaiseLevel(float level)
    {
        try
        {
            LevelChanged?.Invoke(this, level);
        }
        catch (Exception ex)
        {
            _logger.Warn("A level handler failed.", ex);
        }
    }

    private void Raise<T>(EventHandler<T>? handler, T args) where T : EventArgs
    {
        try
        {
            handler?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.Warn("A dictation event handler failed.", ex);
        }
    }
}
