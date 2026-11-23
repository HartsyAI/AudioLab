namespace AudioLab.Audio;

/// <summary>One layer of a long convolution, done in the frequency domain on blocks of one length, with its work spread over each block.</summary>
/// <remarks>
/// The stage takes the taps from two blocks on. A block of input is transformed during the block after it, the sums of spectra are made in
/// slices, and the inverse transform comes last, all in time for the block after that, so no sample does more than one slice of work.
/// </remarks>
internal sealed class FrequencyStage
{
    // The most slices the sum of spectra is cut into.
    private const int MaxSumSlices = 16;

    private readonly int _block;
    private readonly int _bins;
    private readonly RealFft _fft;
    private readonly float[][] _partitionReal;
    private readonly float[][] _partitionImaginary;
    private readonly float[][] _historyReal;
    private readonly float[][] _historyImaginary;
    private readonly int[] _taskStart;
    private readonly int _sliceSize;
    private readonly float[] _padded;
    private readonly float[] _result;
    private readonly float[] _overlap;
    private readonly float[] _sumReal;
    private readonly float[] _sumImaginary;
    private float[] _tail;
    private float[] _nextTail;
    private int _position;
    private int _newest;
    private int _task;

    /// <summary>Builds the stage for the taps from twice <paramref name="block"/> up to <paramref name="end"/> of an impulse response.</summary>
    public FrequencyStage(int block, ReadOnlySpan<float> impulse, int end)
    {
        _block = block;
        _fft = new RealFft(2 * block);
        _bins = _fft.Bins;
        _padded = new float[2 * block];
        _tail = new float[block];
        _nextTail = new float[block];
        _overlap = new float[block];
        _result = new float[2 * block];
        _sumReal = new float[_bins];
        _sumImaginary = new float[_bins];
        int last = Math.Min(end, impulse.Length);
        int first = 2 * block;
        int partitions = last > first ? (last - first + block - 1) / block : 0;
        _partitionReal = new float[partitions][];
        _partitionImaginary = new float[partitions][];
        _historyReal = new float[partitions][];
        _historyImaginary = new float[partitions][];
        float[] segment = new float[2 * block];
        for (int j = 0; j < partitions; j++)
        {
            Array.Clear(segment);
            int from = (j + 2) * block;
            impulse[from..Math.Min(last, from + block)].CopyTo(segment);
            _partitionReal[j] = new float[_bins];
            _partitionImaginary[j] = new float[_bins];
            _fft.Forward(segment, _partitionReal[j], _partitionImaginary[j]);
            _historyReal[j] = new float[_bins];
            _historyImaginary[j] = new float[_bins];
        }
        int slices = Math.Min(MaxSumSlices, Math.Max(1, partitions));
        _sliceSize = (partitions + slices - 1) / slices;
        // The tasks are the forward transform, the slices of the sum and the inverse transform, spread evenly over the block.
        int tasks = partitions == 0 ? 0 : 2 + ((partitions + _sliceSize - 1) / _sliceSize);
        _taskStart = new int[tasks];
        for (int t = 0; t < tasks; t++)
        {
            _taskStart[t] = (int)((long)t * block / tasks);
        }
        _task = tasks;
    }

    /// <summary>Whether the stage holds no taps.</summary>
    public bool IsEmpty => _partitionReal.Length == 0;

    /// <summary>Forgets all input, so the next sample is the first.</summary>
    public void Reset()
    {
        Array.Clear(_padded);
        Array.Clear(_tail);
        Array.Clear(_nextTail);
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
        _task = _taskStart.Length;
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
            (_tail, _nextTail) = (_nextTail, _tail);
            _task = 0;
        }
        while (_task < _taskStart.Length && _taskStart[_task] <= _position)
        {
            Run(_task);
            _task++;
        }
        return output;
    }

    // Task 0 transforms the block that has just filled, the last task turns the sum back, and those between add up the products.
    private void Run(int task)
    {
        int partitions = _partitionReal.Length;
        if (task == 0)
        {
            _newest = _newest == 0 ? partitions - 1 : _newest - 1;
            _fft.Forward(_padded, _historyReal[_newest], _historyImaginary[_newest]);
            Array.Clear(_sumReal);
            Array.Clear(_sumImaginary);
        }
        else if (task < _taskStart.Length - 1)
        {
            int from = (task - 1) * _sliceSize;
            int to = Math.Min(partitions, from + _sliceSize);
            for (int j = from; j < to; j++)
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
        }
        else
        {
            _fft.Inverse(_sumReal, _sumImaginary, _result);
            for (int k = 0; k < _block; k++)
            {
                _nextTail[k] = _result[k] + _overlap[k];
                _overlap[k] = _result[_block + k];
            }
        }
    }
}
