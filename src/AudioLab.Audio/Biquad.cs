namespace AudioLab.Audio;

/// <summary>A second-order filter section on one channel, in transposed direct form II with double-precision state.</summary>
internal sealed class Biquad
{
    private readonly BiquadCoefficients _coefficients;
    private double _s1;
    private double _s2;

    /// <summary>Makes a section with the given coefficients and nothing in its state.</summary>
    public Biquad(BiquadCoefficients coefficients) => _coefficients = coefficients;

    /// <summary>Forgets the signal that went through.</summary>
    public void Reset()
    {
        _s1 = 0;
        _s2 = 0;
    }

    /// <summary>Filters one sample.</summary>
    public float Process(float input)
    {
        BiquadCoefficients c = _coefficients;
        double x = input;
        double y = (c.B0 * x) + _s1;
        _s1 = (c.B1 * x) - (c.A1 * y) + _s2;
        _s2 = (c.B2 * x) - (c.A2 * y);
        return (float)y;
    }

    /// <summary>Filters a block in place.</summary>
    public void Process(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Process(samples[i]);
        }
    }
}
