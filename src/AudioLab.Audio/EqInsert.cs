using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>The five-band equaliser: high-pass, low shelf, mid peak, high shelf and low-pass in series, as the old DAW chained them.</summary>
internal sealed class EqInsert : IInsert
{
    // The old filters took 0.707 as a Q in decibels, which is what Web Audio defines for the pass filters.
    private const double PassQDb = 0.707;

    private readonly Biquad[] _left;
    private readonly Biquad[] _right;

    public EqInsert(EqEffect effect, int sampleRate)
    {
        BiquadCoefficients[] sections =
        [
            BiquadCoefficients.HighPass(effect.HpFreq, PassQDb, sampleRate),
            BiquadCoefficients.LowShelf(effect.LowFreq, effect.LowGain, sampleRate),
            BiquadCoefficients.Peaking(effect.MidFreq, effect.MidGain, effect.MidQ, sampleRate),
            BiquadCoefficients.HighShelf(effect.HighFreq, effect.HighGain, sampleRate),
            BiquadCoefficients.LowPass(effect.LpFreq, PassQDb, sampleRate),
        ];
        // A section that changes nothing is left out, so a flat band costs no time.
        _left = [.. sections.Where(section => section != BiquadCoefficients.Identity).Select(section => new Biquad(section))];
        _right = [.. sections.Where(section => section != BiquadCoefficients.Identity).Select(section => new Biquad(section))];
    }

    public double TailSeconds => 0;

    public void Reset()
    {
        foreach (Biquad section in _left)
        {
            section.Reset();
        }
        foreach (Biquad section in _right)
        {
            section.Reset();
        }
    }

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
        foreach (Biquad section in _left)
        {
            section.Process(left);
        }
        foreach (Biquad section in _right)
        {
            section.Process(right);
        }
    }
}
