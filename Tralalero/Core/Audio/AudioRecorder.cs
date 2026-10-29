using NAudio;
using NAudio.Wave;

namespace Tralalero.Core.Audio;

/// <summary>A microphone available to Windows.</summary>
public sealed record AudioDeviceInfo(int DeviceNumber, string Name, int Channels)
{
    public override string ToString() => Name;
}

/// <summary>Audio chunk in 16-bit PCM, 16 kHz, mono.</summary>
public sealed class AudioChunkEventArgs : EventArgs
{
    public AudioChunkEventArgs(byte[] buffer)
    {
        Buffer = buffer;
    }

    public byte[] Buffer { get; }
}

public sealed class AudioCaptureException : Exception
{
    public AudioCaptureException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public interface IAudioRecorder : IDisposable
{
    event EventHandler<AudioChunkEventArgs>? AudioAvailable;
    event EventHandler<float>? LevelChanged;

    bool IsRecording { get; }

    void Start(int deviceNumber);

    Task StopAsync();
}

/// <summary>NAudio wrapper that captures the microphone at Whisper-friendly 16 kHz mono.</summary>
public sealed class AudioRecorder : IAudioRecorder
{
    private readonly object _gate = new();
    private WaveIn? _waveIn;
    private TaskCompletionSource? _stopped;
    private bool _disposed;

    public event EventHandler<AudioChunkEventArgs>? AudioAvailable;
    public event EventHandler<float>? LevelChanged;

    public bool IsRecording { get; private set; }

    public static IReadOnlyList<AudioDeviceInfo> GetDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            for (var i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                devices.Add(new AudioDeviceInfo(i, capabilities.ProductName, capabilities.Channels));
            }
        }
        catch (Exception ex) when (ex is MmException or InvalidOperationException)
        {
            // The device list is best-effort; the start path reports a useful error.
        }

        return devices;
    }

    public void Start(int deviceNumber)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRecording)
        {
            throw new InvalidOperationException("A recording is already in progress.");
        }

        var deviceCount = WaveIn.DeviceCount;
        if (deviceCount <= 0)
        {
            throw new AudioCaptureException(
                "No microphone was found. Connect one and check Windows microphone privacy settings.");
        }

        if (deviceNumber < 0 || deviceNumber >= deviceCount)
        {
            deviceNumber = 0;
        }

        _waveIn = new WaveIn
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50,
            NumberOfBuffers = 4
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        try
        {
            _waveIn.StartRecording();
            IsRecording = true;
        }
        catch (Exception ex) when (ex is MmException or InvalidOperationException)
        {
            DisposeWaveIn();
            throw new AudioCaptureException(
                "The microphone could not be started. Check that it is connected and allowed in Windows settings.",
                ex);
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (!IsRecording || _waveIn is null)
            {
                return Task.CompletedTask;
            }

            _stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waveIn.StopRecording();
            return _stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (IsRecording)
            {
                _waveIn?.StopRecording();
            }
        }
        catch
        {
            // Best-effort shutdown.
        }

        DisposeWaveIn();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
        AudioAvailable?.Invoke(this, new AudioChunkEventArgs(buffer));

        var peak = 0f;
        for (var i = 0; i + 1 < buffer.Length; i += 2)
        {
            var sample = Math.Abs(BitConverter.ToInt16(buffer, i) / 32768f);
            if (sample > peak)
            {
                peak = sample;
            }
        }

        LevelChanged?.Invoke(this, peak);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsRecording = false;
        _stopped?.TrySetResult();
        _stopped = null;

        if (e.Exception is not null)
        {
            // The coordinator observes the stopped state; the exception is logged there.
        }
    }

    private void DisposeWaveIn()
    {
        if (_waveIn is null)
        {
            return;
        }

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;
        try
        {
            _waveIn.Dispose();
        }
        catch
        {
            // Best-effort cleanup.
        }

        _waveIn = null;
        IsRecording = false;
    }
}
