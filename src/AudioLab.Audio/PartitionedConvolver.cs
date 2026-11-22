using System.Numerics;

namespace AudioLab.Audio;

/// <summary>Convolves one channel with a long impulse response with no delay.</summary>
/// <remarks>
/// The first block of taps is a direct sum over the recent input, the next ones are multiplied in the frequency domain in short
/// blocks, and the long rest in long blocks, which costs far less. It works from a sample counter of its own, so the output does not
/// depend on how the caller cuts the input into blocks.
/// </remarks>
internal sealed class PartitionedConvolver
{
    /// <summary>The length, in taps, of the direct head and the block of the short stage.</summary>
    public const int Block = 128;

    /// <summary>The block of the long stage, which takes the taps from here on.</summary>
    public const int LongBlock = 4096;

    private readonly float[] _head = new float[Block];
    private readonly float[] _history = new float[2 * Block];
    private readonly FrequencyStage _short;
    private readonly FrequencyStage _long;
    private int _position;

    public PartitionedConvolver(ReadOnlySpan<float> impulse)
    {
        if (impulse.Length == 0)
        {
            throw new ArgumentException("The impulse response needs at least one frame.", nameof(impulse));
        }
        // The head is stored backwards, so that it lines up with the window of input that runs oldest to newest.
        for (int k = 0; k < Block && k < impulse.Length; k++)
        {
            _head[Block - 1 - k] = impulse[k];
        }
        _short = new FrequencyStage(Block, impulse, LongBlock);
        _long = new FrequencyStage(LongBlock, impulse, impulse.Length);
    }

    /// <summary>Forgets all input, so the next sample is the first.</summary>
    public void Reset()
    {
        Array.Clear(_history);
        _short.Reset();
        _long.Reset();
        _position = 0;
    }

    /// <summary>Replaces each sample with the convolution's output for it.</summary>
    public void Process(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float x = samples[i];
            // Every sample is written twice, Block apart, so the last Block samples are always in one run.
            _history[_position] = x;
            _history[_position + Block] = x;
            float output = Direct(_position + 1);
            if (!_short.IsEmpty)
            {
                output += _short.Next(x);
            }
            if (!_long.IsEmpty)
            {
                output += _long.Next(x);
            }
            samples[i] = output;
            _position = _position + 1 == Block ? 0 : _position + 1;
        }
    }

    private float Direct(int windowStart)
    {
        ReadOnlySpan<float> window = _history.AsSpan(windowStart, Block);
        Vector<float> sum = Vector<float>.Zero;
        int lanes = Vector<float>.Count;
        for (int k = 0; k < Block; k += lanes)
        {
            sum += new Vector<float>(window[k..]) * new Vector<float>(_head.AsSpan(k));
        }
        return Vector.Sum(sum);
    }
}
