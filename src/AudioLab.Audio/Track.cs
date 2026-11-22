namespace AudioLab.Audio;

/// <summary>One mixer track: a source, two filter sections a channel, a fader, a pan and a reverb send.</summary>
public sealed class Track
{
    private readonly ITrackSource _source;

    private readonly int _sampleRate;

    private readonly float[] _left;

    private readonly float[] _right;

    private readonly BiquadFilter _lowLeft = new();

    private readonly BiquadFilter _lowRight = new();

    private readonly BiquadFilter _peakLeft = new();

    private readonly BiquadFilter _peakRight = new();

    private readonly float _modulationPhase;

    private float _currentLeft;

    private float _currentRight;

    private bool _gainsSet;

    /// <summary>Creates a track that renders at most <paramref name="maxFrames" /> frames at a time.</summary>
    public Track(ITrackSource source, int sampleRate, int maxFrames, float cutoff)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cutoff);
        _source = source;
        _sampleRate = sampleRate;
        _left = new float[maxFrames];
        _right = new float[maxFrames];
        Cutoff = cutoff;
        _modulationPhase = cutoff * 0.001f;
        _peakLeft.SetPeaking(1000, 1.2f, 3, sampleRate);
        _peakRight.SetPeaking(1000, 1.2f, 3, sampleRate);
        Modulate(0);
    }

    /// <summary>The fader, 1 for unity.</summary>
    public float Gain { get; set; } = 0.5f;

    /// <summary>The pan, from -1 (left) to 1 (right).</summary>
    public float Pan { get; set; }

    /// <summary>How much of the track goes to the reverb, from 0.</summary>
    public float ReverbSend { get; set; } = 0.2f;

    /// <summary>The low-pass cutoff the modulation moves around, in hertz.</summary>
    public float Cutoff { get; }

    /// <summary>The largest sample the track has produced since it was created.</summary>
    public float Peak { get; private set; }

    /// <summary>Moves the low-pass cutoff with a slow sine, as an automation lane would.</summary>
    public void Modulate(double seconds)
    {
        float cutoff = Cutoff * (1 + (0.5f * MathF.Sin((float)((seconds * 1.3) + _modulationPhase))));
        _lowLeft.SetLowPass(cutoff, 0.9f, _sampleRate);
        _lowRight.SetLowPass(cutoff, 0.9f, _sampleRate);
    }

    /// <summary>Renders <paramref name="frames" /> frames and adds them to the mix and to the reverb send.</summary>
    public void Mix(int frames, Span<float> mixLeft, Span<float> mixRight, Span<float> reverbSend)
    {
        Span<float> left = _left.AsSpan(0, frames);
        Span<float> right = _right.AsSpan(0, frames);
        _source.Render(left, right);
        _lowLeft.Process(left);
        _lowRight.Process(right);
        _peakLeft.Process(left);
        _peakRight.Process(right);
        float angle = (Pan + 1) * MathF.PI / 4;
        float targetLeft = MathF.Cos(angle) * Gain;
        float targetRight = MathF.Sin(angle) * Gain;
        if (!_gainsSet)
        {
            _currentLeft = targetLeft;
            _currentRight = targetRight;
            _gainsSet = true;
        }
        float stepLeft = (targetLeft - _currentLeft) / frames;
        float stepRight = (targetRight - _currentRight) / frames;
        float gainLeft = _currentLeft;
        float gainRight = _currentRight;
        float peak = Peak;
        for (int i = 0; i < frames; i++)
        {
            gainLeft += stepLeft;
            gainRight += stepRight;
            float outLeft = left[i] * gainLeft;
            float outRight = right[i] * gainRight;
            mixLeft[i] += outLeft;
            mixRight[i] += outRight;
            reverbSend[i] += (outLeft + outRight) * ReverbSend;
            peak = MathF.Max(peak, MathF.Abs(outLeft));
        }
        Peak = peak;
        _currentLeft = targetLeft;
        _currentRight = targetRight;
    }
}
