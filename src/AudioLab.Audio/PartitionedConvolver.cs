using System.Numerics;

namespace AudioLab.Audio;

/// <summary>Convolves one channel with a long impulse response with no delay.</summary>
/// <remarks>
/// The first taps are a direct sum over the recent input, the next ones are multiplied in the frequency domain in short blocks, and the
/// long rest in long blocks, which costs far less. Each frequency stage spreads its work over its block. It works from a sample counter of
/// its own, so the output does not depend on how the caller cuts the input into blocks.
/// </remarks>
internal sealed class PartitionedConvolver
{
    /// <summary>The block of the short stage, which is also a handy length to work in.</summary>
    public const int Block = 128;

    /// <summary>The block of the long stage.</summary>
    public const int LongBlock = 2048;

    // Each stage takes its taps from two of its own blocks on, which is what gives its work a whole block of slack.
    private const int HeadTaps = 2 * Block;
    private const int LongStageStart = 2 * LongBlock;

    private readonly float[] _head = new float[HeadTaps];
    private readonly float[] _history = new float[2 * HeadTaps];
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
        for (int k = 0; k < HeadTaps && k < impulse.Length; k++)
        {
            _head[HeadTaps - 1 - k] = impulse[k];
        }
        _short = new FrequencyStage(Block, impulse, LongStageStart);
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
            // Every sample is written twice, a head apart, so the last HeadTaps samples are always in one run.
            _history[_position] = x;
            _history[_position + HeadTaps] = x;
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
            _position = _position + 1 == HeadTaps ? 0 : _position + 1;
        }
    }

    private float Direct(int windowStart)
    {
        ReadOnlySpan<float> window = _history.AsSpan(windowStart, HeadTaps);
        Vector<float> sum = Vector<float>.Zero;
        int lanes = Vector<float>.Count;
        for (int k = 0; k < HeadTaps; k += lanes)
        {
            sum += new Vector<float>(window[k..]) * new Vector<float>(_head.AsSpan(k));
        }
        return Vector.Sum(sum);
    }
}
