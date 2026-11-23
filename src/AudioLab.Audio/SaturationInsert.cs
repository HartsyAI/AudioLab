using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>A waveshaper, tanh(k x) / tanh(k) with k = 1 + 9 drive, on an input held to -1..1 at four times the rate.</summary>
/// <remarks>
/// Two half-band stages take the signal up to four times the rate and back, so the harmonics the curve makes above the rate are filtered
/// away before they can fold down. The filters delay the wet signal by <see cref="LatencyFrames"/> frames; the dry signal is delayed to
/// match, so the mix blends cleanly, and the effect as a whole is that much late.
/// </remarks>
internal sealed class SaturationInsert : IInsert
{
    /// <summary>How many frames the effect delays its output, whatever the sample rate.</summary>
    public const int Latency = 18;

    private const int FirstStageTaps = 31;
    private const int SecondStageTaps = 13;
    private const double FirstStageBeta = 5.9;
    private const double SecondStageBeta = 5.8;

    private static readonly float[] _firstTaps = HalfbandCoefficients.Create(FirstStageTaps, FirstStageBeta);
    private static readonly float[] _secondTaps = HalfbandCoefficients.Create(SecondStageTaps, SecondStageBeta);

    private readonly double _k;
    private readonly double _norm;
    private readonly float _dry;
    private readonly float _wet;
    private readonly Channel _left = new();
    private readonly Channel _right = new();

    public SaturationInsert(SaturationEffect effect, int sampleRate)
    {
        _k = 1 + (effect.Drive * 9);
        _norm = Math.Tanh(_k);
        _dry = (float)(1 - effect.Mix);
        _wet = (float)(effect.Mix * Math.Pow(10, effect.Trim / 20));
        TailSeconds = (double)Latency / sampleRate;
    }

    public double TailSeconds { get; }

    public int LatencyFrames => Latency;

    public void Reset()
    {
        _left.Reset();
        _right.Reset();
    }

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
        for (int i = 0; i < left.Length; i++)
        {
            left[i] = _left.Process(left[i], this);
            right[i] = _right.Process(right[i], this);
        }
    }

    private float Shape(float input)
    {
        double x = Math.Clamp(input, -1f, 1f);
        return (float)(Math.Tanh(_k * x) / _norm);
    }

    // The state of one channel: the filters of the two stages and the ring that delays the dry signal.
    private sealed class Channel
    {
        private readonly HalfbandResampler _first = new(_firstTaps);
        private readonly HalfbandResampler _second = new(_secondTaps);
        private readonly float[] _dryLine = new float[Latency];
        private int _write;

        public void Reset()
        {
            _first.Reset();
            _second.Reset();
            Array.Clear(_dryLine);
            _write = 0;
        }

        public float Process(float input, SaturationInsert owner)
        {
            (float a0, float a1) = _first.Up(input);
            (float b00, float b01) = _second.Up(a0);
            (float b10, float b11) = _second.Up(a1);
            float c0 = _second.Down(owner.Shape(b00), owner.Shape(b01));
            float c1 = _second.Down(owner.Shape(b10), owner.Shape(b11));
            float wet = _first.Down(c0, c1);
            float dry = _dryLine[_write];
            _dryLine[_write] = input;
            _write = _write + 1 == _dryLine.Length ? 0 : _write + 1;
            return (dry * owner._dry) + (wet * owner._wet);
        }
    }
}
