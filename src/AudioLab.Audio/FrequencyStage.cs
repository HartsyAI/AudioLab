namespace AudioLab.Audio;

/// <summary>One layer of a long convolution, done in the frequency domain on blocks of one length.</summary>
/// <remarks>
/// It takes the taps from one block on, cut into blocks and multiplied in the frequency domain against the spectra of the input blocks
/// that came before, so it needs no input beyond what it has already been given.
/// </remarks>
internal sealed class FrequencyStage
{
    private readonly int _block;
    private readonly int _bins;
    private readonly RealFft _fft;
    private readonly float[][] _partitionReal;
    private readonly float[][] _partitionImaginary;
    private readonly float[][] _historyReal;
    private readonly float[][] _historyImaginary;
    private readonly float[] _padded;
    private readonly float[] _tail;
    private readonly float[] _overlap;
    private readonly float[] _result;
    private readonly float[] _sumReal;
    private readonly float[] _sumImaginary;
    private int _position;
    private int _newest;

    /// <summary>Builds the stage for the taps from <paramref name="block"/> up to <paramref name="end"/> of an impulse response.</summary>
    public FrequencyStage(int block, ReadOnlySpan<float> impulse, int end)
    {
        _block = block;
        _fft = new RealFft(2 * block);
        _bins = _fft.Bins;
        _padded = new float[2 * block];
        _tail = new float[block];
        _overlap = new float[block];
        _result = new float[2 * block];
        _sumReal = new float[_bins];
        _sumImaginary = new float[_bins];
        int last = Math.Min(end, impulse.Length);
        int partitions = last > block ? (last - block + block - 1) / block : 0;
        _partitionReal = new float[partitions][];
        _partitionImaginary = new float[partitions][];
        _historyReal = new float[partitions][];
        _historyImaginary = new float[partitions][];
        float[] segment = new float[2 * block];
        for (int j = 0; j < partitions; j++)
        {
            Array.Clear(segment);
            int from = (j + 1) * block;
            impulse[from..Math.Min(last, from + block)].CopyTo(segment);
            _partitionReal[j] = new float[_bins];
            _partitionImaginary[j] = new float[_bins];
            _fft.Forward(segment, _partitionReal[j], _partitionImaginary[j]);
            _historyReal[j] = new float[_bins];
            _historyImaginary[j] = new float[_bins];
        }
    }

    /// <summary>Whether the stage holds no taps.</summary>
    public bool IsEmpty => _partitionReal.Length == 0;

    /// <summary>Forgets all input, so the next sample is the first.</summary>
    public void Reset()
    {
        Array.Clear(_padded);
        Array.Clear(_tail);
        Array.Clear(_overlap);
        foreach (float[] spectrum in _historyReal)
        {
            Array.Clear(spectrum);
        }
        foreach (float[] spectrum in _historyImaginary)
        {
            Array.Clear(spectrum);
        }
        _position = 0;
        _newest = 0;
    }

    /// <summary>Takes one input sample and gives the stage's contribution to the output at the same moment.</summary>
    public float Next(float input)
    {
        _padded[_position] = input;
        float output = _tail[_position];
        _position++;
        if (_position == _block)
        {
            _position = 0;
            NextTail();
        }
        return output;
    }

    // The input block that has just filled becomes the newest spectrum, and the tail of the next block is the sum of every
    // spectrum against the partition that lines up with it.
    private void NextTail()
    {
        int partitions = _partitionReal.Length;
        _newest = _newest == 0 ? partitions - 1 : _newest - 1;
        _fft.Forward(_padded, _historyReal[_newest], _historyImaginary[_newest]);
        Array.Clear(_sumReal);
        Array.Clear(_sumImaginary);
        for (int j = 0; j < partitions; j++)
        {
            int slot = _newest + j;
            if (slot >= partitions)
            {
                slot -= partitions;
            }
            SpectrumMath.MultiplyAdd(
                _historyReal[slot],
                _historyImaginary[slot],
                _partitionReal[j],
                _partitionImaginary[j],
                _sumReal,
                _sumImaginary);
        }
        _fft.Inverse(_sumReal, _sumImaginary, _result);
        for (int k = 0; k < _block; k++)
        {
            _tail[k] = _result[k] + _overlap[k];
            _overlap[k] = _result[_block + k];
        }
    }
}
