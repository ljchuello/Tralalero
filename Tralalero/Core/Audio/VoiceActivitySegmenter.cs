using Tralalero.Core.Transcription;

namespace Tralalero.Core.Audio;

/// <summary>
/// Energy-based voice activity detector with adaptive noise floor.
/// It turns a continuous 16 kHz PCM stream into phrase-sized WAV segments.
/// </summary>
public sealed class VoiceActivitySegmenter
{
    private readonly int _sampleRate;
    private readonly int _frameBytes;
    private readonly int _frameMilliseconds = 20;
    private readonly int _preRollFrames;
    private readonly int _hangoverFrames;
    private readonly int _tailFrames;
    private readonly int _minVoicedFrames;
    private readonly int _maxFrames;
    private readonly int _startFrames = 3;
    private readonly int _calibrationFrames = 25;

    private readonly List<byte> _pending = new();
    private readonly Queue<byte[]> _preRoll = new();
    private readonly List<byte[]> _segmentFrames = new();
    private readonly List<double> _calibrationRms = new();

    private bool _calibrated;
    private double _noiseFloor = 0.004;
    private bool _inSpeech;
    private int _voicedFrames;
    private int _voicedFrameCount;
    private int _silenceFrames;
    private int _segmentFrameCount;

    public VoiceActivitySegmenter(VadSensitivity sensitivity, int sampleRate = 16000)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        _sampleRate = sampleRate;
        _frameBytes = sampleRate * 2 * _frameMilliseconds / 1000;
        _preRollFrames = 300 / _frameMilliseconds;
        _hangoverFrames = 700 / _frameMilliseconds;
        _tailFrames = 200 / _frameMilliseconds;
        _minVoicedFrames = 500 / _frameMilliseconds;
        _maxFrames = 20000 / _frameMilliseconds;
        Sensitivity = sensitivity;
    }

    public VadSensitivity Sensitivity { get; }

    public double NoiseFloor => _noiseFloor;

    public bool IsInSpeech => _inSpeech;

    /// <summary>Feeds arbitrary-sized PCM chunks and returns any completed phrases.</summary>
    public IReadOnlyList<AudioSegment> Push(ReadOnlySpan<byte> pcm)
    {
        if (pcm.IsEmpty)
        {
            return Array.Empty<AudioSegment>();
        }

        _pending.AddRange(pcm.ToArray());

        var results = new List<AudioSegment>();
        while (_pending.Count >= _frameBytes)
        {
            var frame = new byte[_frameBytes];
            _pending.CopyTo(0, frame, 0, _frameBytes);
            _pending.RemoveRange(0, _frameBytes);

            var segment = ProcessFrame(frame);
            if (segment is not null)
            {
                results.Add(segment);
            }
        }

        return results;
    }

    /// <summary>Flushes a partial frame and emits the phrase currently being spoken.</summary>
    public IReadOnlyList<AudioSegment> Flush()
    {
        var results = new List<AudioSegment>();

        if (_pending.Count > 0)
        {
            var frame = new byte[_frameBytes];
            _pending.CopyTo(frame, 0);
            _pending.Clear();

            var segment = ProcessFrame(frame);
            if (segment is not null)
            {
                results.Add(segment);
            }
        }

        if (_inSpeech && _voicedFrameCount >= _minVoicedFrames)
        {
            var segment = EmitSegment(trimTrailingSilence: true);
            if (segment is not null)
            {
                results.Add(segment);
            }
        }

        Reset();
        return results;
    }

    public void Reset()
    {
        _pending.Clear();
        _preRoll.Clear();
        _segmentFrames.Clear();
        _calibrationRms.Clear();
        _calibrated = false;
        _noiseFloor = 0.004;
        _inSpeech = false;
        _voicedFrames = 0;
        _voicedFrameCount = 0;
        _silenceFrames = 0;
        _segmentFrameCount = 0;
    }

    private AudioSegment? ProcessFrame(byte[] frame)
    {
        var rms = ComputeRms(frame);

        if (!_calibrated)
        {
            _calibrationRms.Add(rms);
            _preRoll.Enqueue(frame);
            TrimPreRoll();

            if (_calibrationRms.Count < _calibrationFrames)
            {
                return null;
            }

            _noiseFloor = Math.Clamp(Percentile(_calibrationRms, 0.2), 0.0015, 0.05);
            _calibrated = true;
            _voicedFrames = 0;
            return null;
        }

        var voiced = rms > Threshold;

        if (!_inSpeech)
        {
            _preRoll.Enqueue(frame);
            TrimPreRoll();

            if (!voiced)
            {
                _voicedFrames = 0;
                return null;
            }

            _voicedFrames++;
            if (_voicedFrames < _startFrames)
            {
                return null;
            }

            _inSpeech = true;
            _segmentFrames.Clear();
            _segmentFrames.AddRange(_preRoll);
            _segmentFrameCount = _segmentFrames.Count;
            _voicedFrameCount = _voicedFrames;
            _silenceFrames = 0;
            _preRoll.Clear();
            return null;
        }

        _segmentFrames.Add(frame);
        _segmentFrameCount++;

        if (voiced)
        {
            _voicedFrameCount++;
            _silenceFrames = 0;
        }
        else
        {
            _silenceFrames++;
        }

        if (_silenceFrames >= _hangoverFrames)
        {
            var segment = EmitSegment(trimTrailingSilence: true);
            _inSpeech = false;
            _preRoll.Clear();
            _voicedFrames = 0;
            _voicedFrameCount = 0;
            _silenceFrames = 0;
            _segmentFrameCount = 0;
            return segment;
        }

        if (_segmentFrameCount >= _maxFrames)
        {
            var segment = EmitSegment(trimTrailingSilence: false);

            // Continue listening immediately after the forced split.
            _segmentFrames.Clear();
            _segmentFrames.Add(frame);
            _segmentFrameCount = 1;
            _voicedFrameCount = voiced ? 1 : 0;
            _silenceFrames = 0;
            return segment;
        }

        return null;
    }

    private AudioSegment? EmitSegment(bool trimTrailingSilence)
    {
        if (_voicedFrameCount < _minVoicedFrames || _segmentFrames.Count == 0)
        {
            return null;
        }

        var frames = new List<byte[]>(_segmentFrames);
        if (trimTrailingSilence && _silenceFrames > _tailFrames)
        {
            var remove = _silenceFrames - _tailFrames;
            var removeCount = Math.Min(remove, frames.Count - 1);
            if (removeCount > 0)
            {
                frames.RemoveRange(frames.Count - removeCount, removeCount);
            }
        }

        if (frames.Count == 0)
        {
            return null;
        }

        var pcm = new byte[frames.Count * _frameBytes];
        for (var i = 0; i < frames.Count; i++)
        {
            Buffer.BlockCopy(frames[i], 0, pcm, i * _frameBytes, _frameBytes);
        }

        var duration = TimeSpan.FromSeconds(pcm.Length / (double)(_sampleRate * 2));
        var wav = WavWriter.CreateWav(pcm, _sampleRate);
        return new AudioSegment(wav, duration, DateTimeOffset.UtcNow);
    }

    private double Threshold
    {
        get
        {
            var multiplier = Sensitivity switch
            {
                VadSensitivity.Low => 3.2,
                VadSensitivity.High => 1.8,
                _ => 2.4
            };

            return Math.Max(_noiseFloor * multiplier, 0.006);
        }
    }

    private static double ComputeRms(byte[] frame)
    {
        var sum = 0.0;
        var samples = frame.Length / 2;
        if (samples == 0)
        {
            return 0;
        }

        for (var i = 0; i + 1 < frame.Length; i += 2)
        {
            var sample = BitConverter.ToInt16(frame, i) / 32768.0;
            sum += sample * sample;
        }

        return Math.Sqrt(sum / samples);
    }

    private static double Percentile(List<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(value => value).ToArray();
        var index = (int)Math.Round((sorted.Length - 1) * Math.Clamp(percentile, 0, 1));
        return sorted[index];
    }

    private void TrimPreRoll()
    {
        while (_preRoll.Count > _preRollFrames)
        {
            _preRoll.Dequeue();
        }
    }
}
