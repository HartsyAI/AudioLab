using System.Diagnostics;

namespace AudioLab.Audio;

/// <summary>How long rendering takes against the audio it makes, kept in fixed storage so it can run on the audio path.</summary>
public sealed class RenderLoad
{
    private const int BinCount = 160;

    private const double BinMilliseconds = 0.25;

    private readonly long[] _bins = new long[BinCount];

    private readonly int _sampleRate;

    /// <summary>Creates a meter for an engine at a sample rate.</summary>
    public RenderLoad(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _sampleRate = sampleRate;
    }

    /// <summary>Calls to render.</summary>
    public long Calls { get; private set; }

    /// <summary>Frames rendered.</summary>
    public long Frames { get; private set; }

    /// <summary>The longest single call, in milliseconds.</summary>
    public double MaxMilliseconds { get; private set; }

    private double TotalMilliseconds { get; set; }

    /// <summary>Adds one render call that took <paramref name="ticks" /> stopwatch ticks for <paramref name="frames" /> frames.</summary>
    public void Record(long ticks, int frames)
    {
        double milliseconds = ticks * 1000.0 / Stopwatch.Frequency;
        Calls++;
        Frames += frames;
        TotalMilliseconds += milliseconds;
        MaxMilliseconds = Math.Max(MaxMilliseconds, milliseconds);
        int bin = Math.Min(BinCount - 1, (int)(milliseconds / BinMilliseconds));
        _bins[bin]++;
    }

    /// <summary>Rendering time as a fraction of the audio time it produced; 1 is the whole core.</summary>
    public double Utilisation()
    {
        double audioMilliseconds = Frames * 1000.0 / _sampleRate;
        return audioMilliseconds <= 0 ? 0 : TotalMilliseconds / audioMilliseconds;
    }

    /// <summary>The call duration under which the given fraction of calls fell, to the bin size.</summary>
    public double PercentileMilliseconds(double fraction)
    {
        if (Calls == 0)
        {
            return 0;
        }
        long target = (long)Math.Ceiling(Calls * fraction);
        long seen = 0;
        for (int bin = 0; bin < BinCount; bin++)
        {
            seen += _bins[bin];
            if (seen >= target)
            {
                return (bin + 1) * BinMilliseconds;
            }
        }
        return BinCount * BinMilliseconds;
    }
}
