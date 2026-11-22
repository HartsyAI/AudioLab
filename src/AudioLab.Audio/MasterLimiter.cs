namespace AudioLab.Audio;

/// <summary>A peak limiter for the master: no delay and unity gain below the threshold, so a mix under it equals the sum of its stems.</summary>
/// <remarks>The gain drops to what the loudest channel needs at once and recovers over the release time, so no sample passes the ceiling.</remarks>
internal sealed class MasterLimiter
{
    /// <summary>The ceiling, in decibels full scale.</summary>
    public const double CeilingDb = -1;

    /// <summary>How long the gain takes to recover by a factor of e, in seconds.</summary>
    public const double ReleaseSeconds = 0.15;

    private readonly float _ceiling = (float)Math.Pow(10, CeilingDb / 20);
    private readonly double _recovery;
    private double _gain = 1;

    public MasterLimiter(int sampleRate) => _recovery = Math.Exp(-1 / (ReleaseSeconds * sampleRate));

    public void Reset() => _gain = 1;

    public void Process(Span<float> left, Span<float> right)
    {
        double gain = _gain;
        for (int i = 0; i < left.Length; i++)
        {
            float peak = Math.Max(Math.Abs(left[i]), Math.Abs(right[i]));
            double wanted = peak > _ceiling ? _ceiling / peak : 1;
            gain = wanted < gain ? wanted : wanted + ((gain - wanted) * _recovery);
            float applied = (float)gain;
            left[i] *= applied;
            right[i] *= applied;
        }
        _gain = gain;
    }
}
