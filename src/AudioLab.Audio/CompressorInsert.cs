using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>A feed-forward compressor with a soft knee, no look-ahead and no automatic make-up gain; the make-up is a setting.</summary>
/// <remarks>The detector follows the louder channel, so the stereo image holds. Attack and release smooth the gain reduction in decibels.</remarks>
internal sealed class CompressorInsert : IInsert
{
    private const double DbPerNeper = 8.685889638065035;
    private const float Silence = 1e-9f;

    private readonly double _threshold;
    private readonly double _knee;
    private readonly double _slope;
    private readonly double _makeup;
    private readonly double _attack;
    private readonly double _release;
    private double _reduction;

    public CompressorInsert(CompressorEffect effect, int sampleRate)
    {
        _threshold = effect.Threshold;
        _knee = effect.Knee;
        _slope = (1 / effect.Ratio) - 1;
        _makeup = effect.Makeup;
        _attack = Math.Exp(-1 / (effect.Attack * sampleRate));
        _release = Math.Exp(-1 / (effect.Release * sampleRate));
    }

    public double TailSeconds => 0;

    public void Reset() => _reduction = 0;

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
        for (int i = 0; i < left.Length; i++)
        {
            float peak = Math.Max(Math.Max(Math.Abs(left[i]), Math.Abs(right[i])), Silence);
            if (!float.IsFinite(peak))
            {
                peak = Silence;
            }
            double target = ReductionFor(DbPerNeper * Math.Log(peak));
            double coefficient = target < _reduction ? _attack : _release;
            _reduction = Denormals.Flush(target + ((_reduction - target) * coefficient));
            float gain = (float)Math.Exp((_reduction + _makeup) / DbPerNeper);
            left[i] *= gain;
            right[i] *= gain;
        }
    }

    // The gain change, in decibels and never positive, that the static curve gives a level.
    private double ReductionFor(double levelDb)
    {
        double over = levelDb - _threshold;
        if (2 * over < -_knee)
        {
            return 0;
        }
        if (_knee > 0 && 2 * Math.Abs(over) <= _knee)
        {
            double inside = over + (_knee / 2);
            return _slope * inside * inside / (2 * _knee);
        }
        return _slope * over;
    }
}
