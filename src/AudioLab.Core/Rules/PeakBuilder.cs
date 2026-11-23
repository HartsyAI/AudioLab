using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Builds the peaks of a file from its samples, a block at a time, so a file never has to be held whole.</summary>
/// <remarks>The finest level is measured from the samples; each coarser level is the minimum and maximum of four peaks of the one below.</remarks>
public sealed class PeakBuilder
{
    private const int LevelStep = 4;

    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly int _framesPerBucket = PeakFile.FramesPerPeak[0];
    private readonly short[] _min;
    private readonly short[] _max;
    private short[] _fine = new short[1024];
    private int _fineLength;
    private int _inBucket;
    private long _frames;

    /// <summary>Starts the peaks of a file with the given shape.</summary>
    public PeakBuilder(int channels, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, MediaValidator.MaxChannels);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, MediaValidator.MinSampleRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, MediaValidator.MaxSampleRate);
        _channels = channels;
        _sampleRate = sampleRate;
        _min = new short[channels];
        _max = new short[channels];
    }

    /// <summary>A sample from -1 to 1 as a peak value: times 32768, rounded to the nearest even, kept from -32768 to 32767.</summary>
    public static short Quantize(float sample)
    {
        return (short)Math.Clamp(Math.Round(sample * 32768f), short.MinValue, short.MaxValue);
    }

    /// <summary>Adds whole frames of interleaved samples.</summary>
    /// <exception cref="ArgumentException">The block is not a whole number of frames.</exception>
    public void AddFrames(ReadOnlySpan<float> interleaved)
    {
        if (interleaved.Length % _channels != 0)
        {
            throw new ArgumentException("A block is a whole number of frames.", nameof(interleaved));
        }
        for (int offset = 0; offset < interleaved.Length; offset += _channels)
        {
            for (int channel = 0; channel < _channels; channel++)
            {
                short value = Quantize(interleaved[offset + channel]);
                if (_inBucket == 0 || value < _min[channel])
                {
                    _min[channel] = value;
                }
                if (_inBucket == 0 || value > _max[channel])
                {
                    _max[channel] = value;
                }
            }
            _inBucket++;
            _frames++;
            if (_inBucket == _framesPerBucket)
            {
                EndBucket();
            }
        }
    }

    /// <summary>Ends the file and returns its peaks at every level.</summary>
    /// <exception cref="InvalidOperationException">No frame was added.</exception>
    public PeakData Build()
    {
        if (_frames == 0)
        {
            throw new InvalidOperationException("A file with no frames has no peaks.");
        }
        if (_inBucket > 0)
        {
            EndBucket();
        }
        List<PeakLevel> levels = [new PeakLevel(PeakFile.FramesPerPeak[0], _fine[.._fineLength])];
        short[] below = levels[0].Values.ToArray();
        for (int index = 1; index < PeakFile.FramesPerPeak.Length; index++)
        {
            below = Reduce(below);
            levels.Add(new PeakLevel(PeakFile.FramesPerPeak[index], below));
        }
        return new PeakData(_channels, _sampleRate, _frames, levels);
    }

    private void EndBucket()
    {
        int needed = _fineLength + (_channels * 2);
        if (needed > _fine.Length)
        {
            Array.Resize(ref _fine, Math.Max(needed, _fine.Length * 2));
        }
        for (int channel = 0; channel < _channels; channel++)
        {
            _fine[_fineLength++] = _min[channel];
            _fine[_fineLength++] = _max[channel];
        }
        _inBucket = 0;
    }

    // Four peaks of the level below make one; the last may be made of fewer.
    private short[] Reduce(short[] below)
    {
        int stride = _channels * 2;
        int peaks = below.Length / stride;
        int coarse = (peaks + LevelStep - 1) / LevelStep;
        short[] result = new short[coarse * stride];
        for (int peak = 0; peak < coarse; peak++)
        {
            int first = peak * LevelStep;
            int last = Math.Min(first + LevelStep, peaks);
            for (int channel = 0; channel < _channels; channel++)
            {
                short min = short.MaxValue;
                short max = short.MinValue;
                for (int source = first; source < last; source++)
                {
                    min = Math.Min(min, below[(source * stride) + (channel * 2)]);
                    max = Math.Max(max, below[(source * stride) + (channel * 2) + 1]);
                }
                result[(peak * stride) + (channel * 2)] = min;
                result[(peak * stride) + (channel * 2) + 1] = max;
            }
        }
        return result;
    }
}
