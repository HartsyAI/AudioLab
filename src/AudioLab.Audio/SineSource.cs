namespace AudioLab.Audio;

/// <summary>A sine with one overtone, the same in both channels.</summary>
public sealed class SineSource : ITrackSource
{
    private readonly double _step;

    private double _phase;

    /// <summary>Creates a source at a frequency in hertz.</summary>
    public SineSource(double frequency, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _step = 2 * Math.PI * frequency / sampleRate;
    }

    /// <inheritdoc />
    public void Render(Span<float> left, Span<float> right)
    {
        double phase = _phase;
        for (int i = 0; i < left.Length; i++)
        {
            float angle = (float)phase;
            float sample = (0.6f * MathF.Sin(angle)) + (0.25f * MathF.Sin(2 * angle));
            left[i] = sample;
            right[i] = sample;
            phase += _step;
            if (phase > 2 * Math.PI)
            {
                phase -= 2 * Math.PI;
            }
        }
        _phase = phase;
    }
}
