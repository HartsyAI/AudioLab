namespace AudioLab.Audio;

/// <summary>A second-order filter section on one channel, in transposed direct form II with double-precision state.</summary>
internal sealed class Biquad
{
    private readonly BiquadCoefficients _coefficients;
    private double _s1;
    private double _s2;

    public Biquad(BiquadCoefficients coefficients) => _coefficients = coefficients;

    public void Reset()
    {
        _s1 = 0;
        _s2 = 0;
    }

    public float Process(float input)
    {
        BiquadCoefficients c = _coefficients;
        double x = input;
        double y = (c.B0 * x) + _s1;
        _s1 = (c.B1 * x) - (c.A1 * y) + _s2;
        _s2 = (c.B2 * x) - (c.A2 * y);
        return (float)y;
    }

    public void Process(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Process(samples[i]);
        }
    }
}
