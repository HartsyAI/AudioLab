namespace AudioLab.Audio;

/// <summary>A Schroeder-Moorer reverb: eight combs and four all-pass sections a side, from a mono send.</summary>
public sealed class Reverb
{
    private const float InputGain = 0.015f;

    private const int StereoSpread = 23;

    private static readonly int[] _combLengths = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617];

    private static readonly int[] _allpassLengths = [556, 441, 341, 225];

    private readonly CombFilter[] _combsLeft;

    private readonly CombFilter[] _combsRight;

    private readonly AllpassFilter[] _allpassLeft;

    private readonly AllpassFilter[] _allpassRight;

    /// <summary>Creates a reverb for a sample rate.</summary>
    public Reverb(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        double scale = sampleRate / 44100.0;
        _combsLeft = new CombFilter[_combLengths.Length];
        _combsRight = new CombFilter[_combLengths.Length];
        for (int i = 0; i < _combLengths.Length; i++)
        {
            _combsLeft[i] = new CombFilter((int)(_combLengths[i] * scale), 0.84f, 0.2f);
            _combsRight[i] = new CombFilter((int)((_combLengths[i] + StereoSpread) * scale), 0.84f, 0.2f);
        }
        _allpassLeft = new AllpassFilter[_allpassLengths.Length];
        _allpassRight = new AllpassFilter[_allpassLengths.Length];
        for (int i = 0; i < _allpassLengths.Length; i++)
        {
            _allpassLeft[i] = new AllpassFilter((int)(_allpassLengths[i] * scale));
            _allpassRight[i] = new AllpassFilter((int)((_allpassLengths[i] + StereoSpread) * scale));
        }
    }

    /// <summary>Adds the reverb of a mono send to the output buffers.</summary>
    public void Process(ReadOnlySpan<float> send, Span<float> left, Span<float> right)
    {
        for (int i = 0; i < send.Length; i++)
        {
            float input = send[i] * InputGain;
            float wetLeft = 0;
            float wetRight = 0;
            for (int comb = 0; comb < _combsLeft.Length; comb++)
            {
                wetLeft += _combsLeft[comb].Process(input);
                wetRight += _combsRight[comb].Process(input);
            }
            for (int stage = 0; stage < _allpassLeft.Length; stage++)
            {
                wetLeft = _allpassLeft[stage].Process(wetLeft);
                wetRight = _allpassRight[stage].Process(wetRight);
            }
            left[i] += wetLeft;
            right[i] += wetRight;
        }
    }
}
