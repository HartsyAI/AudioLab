using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>A convolution reverb: a predelay, then each channel convolved with its own generated impulse response, mixed with the dry.</summary>
internal sealed class ReverbInsert : IInsert
{
    private readonly PartitionedConvolver _convolverLeft;
    private readonly PartitionedConvolver _convolverRight;
    private readonly float[] _predelayLeft;
    private readonly float[] _predelayRight;
    private readonly float[] _wetLeft;
    private readonly float[] _wetRight;
    private readonly int _predelayFrames;
    private readonly float _dry;
    private readonly float _wet;
    private int _write;

    public ReverbInsert(ReverbEffect effect, int sampleRate)
    {
        float[] left = ReverbImpulse.Create(effect.Size, effect.Decay, 0, sampleRate);
        float[] right = ReverbImpulse.Create(effect.Size, effect.Decay, 1, sampleRate);
        float scale = (float)ReverbImpulse.Scale(left, right, sampleRate);
        for (int i = 0; i < left.Length; i++)
        {
            left[i] *= scale;
            right[i] *= scale;
        }
        _convolverLeft = new PartitionedConvolver(left);
        _convolverRight = new PartitionedConvolver(right);
        _predelayFrames = (int)Math.Round(effect.Predelay * sampleRate);
        _predelayLeft = new float[_predelayFrames + 1];
        _predelayRight = new float[_predelayFrames + 1];
        _wetLeft = new float[PartitionedConvolver.Block];
        _wetRight = new float[PartitionedConvolver.Block];
        _dry = (float)(1 - effect.Mix);
        _wet = (float)effect.Mix;
        TailSeconds = TailFor(effect, sampleRate);
    }

    public double TailSeconds { get; }

    /// <summary>How long a reverb rings after its input stops: its predelay and the length of its room.</summary>
    public static double TailFor(ReverbEffect effect, int sampleRate) =>
        effect.Predelay + ((double)ReverbImpulse.LengthFor(effect.Size, sampleRate) / sampleRate);

    public void Reset()
    {
        _convolverLeft.Reset();
        _convolverRight.Reset();
        Array.Clear(_predelayLeft);
        Array.Clear(_predelayRight);
        _write = 0;
    }

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
        int done = 0;
        while (done < left.Length)
        {
            int count = Math.Min(PartitionedConvolver.Block, left.Length - done);
            ProcessBlock(left.Slice(done, count), right.Slice(done, count));
            done += count;
        }
    }

    private void ProcessBlock(Span<float> left, Span<float> right)
    {
        Span<float> wetLeft = _wetLeft.AsSpan(0, left.Length);
        Span<float> wetRight = _wetRight.AsSpan(0, left.Length);
        for (int i = 0; i < left.Length; i++)
        {
            _predelayLeft[_write] = left[i];
            _predelayRight[_write] = right[i];
            int read = _write - _predelayFrames;
            if (read < 0)
            {
                read += _predelayLeft.Length;
            }
            wetLeft[i] = _predelayLeft[read];
            wetRight[i] = _predelayRight[read];
            _write = _write + 1 == _predelayLeft.Length ? 0 : _write + 1;
        }
        _convolverLeft.Process(wetLeft);
        _convolverRight.Process(wetRight);
        for (int i = 0; i < left.Length; i++)
        {
            left[i] = (left[i] * _dry) + (wetLeft[i] * _wet);
            right[i] = (right[i] * _dry) + (wetRight[i] * _wet);
        }
    }
}
