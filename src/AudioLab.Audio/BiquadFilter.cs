namespace AudioLab.Audio;

/// <summary>A second-order filter section in direct form II transposed, one channel.</summary>
public sealed class BiquadFilter
{
    private float _b0 = 1;

    private float _b1;

    private float _b2;

    private float _a1;

    private float _a2;

    private float _z1;

    private float _z2;

    /// <summary>Sets a low-pass response. Allocation-free, safe between blocks.</summary>
    public void SetLowPass(float frequency, float q, float sampleRate)
    {
        float omega = 2 * MathF.PI * frequency / sampleRate;
        float cos = MathF.Cos(omega);
        float alpha = MathF.Sin(omega) / (2 * q);
        float a0 = 1 + alpha;
        _b0 = (1 - cos) / 2 / a0;
        _b1 = (1 - cos) / a0;
        _b2 = _b0;
        _a1 = -2 * cos / a0;
        _a2 = (1 - alpha) / a0;
    }

    /// <summary>Sets a peaking-EQ response. Allocation-free, safe between blocks.</summary>
    public void SetPeaking(float frequency, float q, float gainDb, float sampleRate)
    {
        float amplitude = MathF.Pow(10, gainDb / 40);
        float omega = 2 * MathF.PI * frequency / sampleRate;
        float cos = MathF.Cos(omega);
        float alpha = MathF.Sin(omega) / (2 * q);
        float a0 = 1 + (alpha / amplitude);
        _b0 = (1 + (alpha * amplitude)) / a0;
        _b1 = -2 * cos / a0;
        _b2 = (1 - (alpha * amplitude)) / a0;
        _a1 = _b1;
        _a2 = (1 - (alpha / amplitude)) / a0;
    }

    /// <summary>Filters the samples in place.</summary>
    public void Process(Span<float> samples)
    {
        float z1 = _z1;
        float z2 = _z2;
        for (int i = 0; i < samples.Length; i++)
        {
            float input = samples[i];
            float output = (_b0 * input) + z1;
            z1 = (_b1 * input) - (_a1 * output) + z2;
            z2 = (_b2 * input) - (_a2 * output);
            samples[i] = output;
        }
        _z1 = z1;
        _z2 = z2;
    }
}
