namespace AudioLab.Audio;

/// <summary>A pair of half-band filters, one that doubles the rate of a stream and one that halves it, which share their taps.</summary>
/// <remarks>
/// Up and then down again, a stream is delayed by half the filter length in its own samples and its level is kept. The filters keep their
/// history in rings and allocate nothing once built.
/// </remarks>
internal sealed class HalfbandResampler
{
    private readonly float[] _taps;
    private readonly int _half;
    private readonly float[] _upHistory;
    private readonly float[] _downHistory;
    private int _upWrite;
    private int _downWrite;

    public HalfbandResampler(float[] taps)
    {
        _taps = taps;
        _half = taps.Length / 2;
        _upHistory = new float[_half + 1];
        _downHistory = new float[taps.Length];
    }

    /// <summary>Forgets everything the filters hold.</summary>
    public void Reset()
    {
        Array.Clear(_upHistory);
        Array.Clear(_downHistory);
        _upWrite = 0;
        _downWrite = 0;
    }

    /// <summary>Takes one sample and gives the two samples at twice the rate that replace it.</summary>
    public (float First, float Second) Up(float input)
    {
        _upHistory[_upWrite] = input;
        int length = _upHistory.Length;
        float even = 0;
        float odd = 0;
        // The taps at even places belong to the first output and those at odd places to the second, which sits half a sample later.
        for (int j = 0; j < _taps.Length; j += 2)
        {
            even += _taps[j] * _upHistory[Wrap(_upWrite - (j / 2), length)];
        }
        for (int j = 1; j < _taps.Length; j += 2)
        {
            odd += _taps[j] * _upHistory[Wrap(_upWrite - ((j - 1) / 2), length)];
        }
        _upWrite = Wrap(_upWrite + 1, length);
        return (2 * even, 2 * odd);
    }

    /// <summary>Takes two samples at twice the rate and gives the one that replaces them.</summary>
    public float Down(float first, float second)
    {
        int length = _downHistory.Length;
        _downHistory[_downWrite] = first;
        float sum = 0;
        for (int j = 0; j < _taps.Length; j++)
        {
            sum += _taps[j] * _downHistory[Wrap(_downWrite - j, length)];
        }
        _downWrite = Wrap(_downWrite + 1, length);
        _downHistory[_downWrite] = second;
        _downWrite = Wrap(_downWrite + 1, length);
        return sum;
    }

    private static int Wrap(int index, int length)
    {
        int wrapped = index % length;
        return wrapped < 0 ? wrapped + length : wrapped;
    }
}
