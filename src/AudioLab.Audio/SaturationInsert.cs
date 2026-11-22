using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>
/// The old waveshaper, tanh(k x) / tanh(k) with k = 1 + 9 drive on an input held to -1..1, then the trim, mixed with the dry signal.
/// It is anti-aliased by the antiderivative of the curve, which costs no delay, where the old one oversampled four times.
/// </summary>
internal sealed class SaturationInsert : IInsert
{
    // Below this change between two samples the slope is read at their midpoint, where the quotient of differences loses precision.
    private const double SmallStep = 1e-6;

    private readonly double _k;
    private readonly double _norm;
    private readonly float _dry;
    private readonly float _wet;
    private double _previousLeft;
    private double _previousRight;

    public SaturationInsert(SaturationEffect effect)
    {
        _k = 1 + (effect.Drive * 9);
        _norm = Math.Tanh(_k);
        _dry = (float)(1 - effect.Mix);
        _wet = (float)(effect.Mix * Math.Pow(10, effect.Trim / 20));
    }

    public double TailSeconds => 0;

    public void Reset()
    {
        _previousLeft = 0;
        _previousRight = 0;
    }

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
        for (int i = 0; i < left.Length; i++)
        {
            float dryLeft = left[i];
            float dryRight = right[i];
            left[i] = (dryLeft * _dry) + ((float)Shape(dryLeft, ref _previousLeft) * _wet);
            right[i] = (dryRight * _dry) + ((float)Shape(dryRight, ref _previousRight) * _wet);
        }
    }

    // First-order antiderivative anti-aliasing: the average of the curve between the last sample and this one.
    private double Shape(double input, ref double previous)
    {
        double x = Math.Clamp(input, -1, 1);
        double step = x - previous;
        double shaped = Math.Abs(step) < SmallStep
            ? Math.Tanh(_k * (x + previous) / 2) / _norm
            : (Integral(x) - Integral(previous)) / step;
        previous = x;
        return shaped;
    }

    // The integral of the curve, ln cosh(k x) / (k tanh k), written so a large argument does not overflow.
    private double Integral(double x)
    {
        double a = Math.Abs(_k * x);
        return (a + Math.Log(1 + Math.Exp(-2 * a)) - Math.Log(2)) / (_k * _norm);
    }
}
