using AudioLab.Core.Model;

namespace AudioLab.Core.Transcription;

/// <summary>The window arithmetic of D91: integer frames at 16 kHz, so the same media always gives the same windows.</summary>
public static class TranscriptionWindows
{
    /// <summary>The sample rate the model is given audio at.</summary>
    public const int SampleRate = 16_000;

    /// <summary>The length of one window, in frames.</summary>
    public const long WindowFrames = 30L * SampleRate;

    /// <summary>How far each window starts after the one before, in frames: the window length less the overlap.</summary>
    public const long StepFrames = 28L * SampleRate;

    /// <summary>The length of one window, in seconds.</summary>
    public const double WindowSeconds = 30;

    /// <summary>The step between windows, in seconds.</summary>
    public const double StepSeconds = 28;

    /// <summary>The number of frames a duration is, rounded to the frame grid.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The duration is negative, not finite or longer than the limit of a media file.</exception>
    public static long FramesOf(double durationSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(durationSeconds, ProjectLimits.MaxSeconds);
        return (long)Math.Round(durationSeconds * SampleRate, MidpointRounding.AwayFromZero);
    }

    /// <summary>How many windows a media file of this duration has; none for an empty file.</summary>
    public static int CountOf(double durationSeconds)
    {
        long frames = FramesOf(durationSeconds);
        return checked((int)((frames + StepFrames - 1) / StepFrames));
    }

    /// <summary>The window at <paramref name="index"/> of a media file of this duration.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is not a window of the file.</exception>
    public static TranscriptionWindow At(int index, double durationSeconds)
    {
        long frames = FramesOf(durationSeconds);
        int count = CountOf(durationSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
        long start = index * StepFrames;
        long length = Math.Min(WindowFrames, frames - start);
        bool last = index == count - 1;
        double ownedEnd = last ? frames / (double)SampleRate : (start + StepFrames) / (double)SampleRate;
        return new TranscriptionWindow(index, start / (double)SampleRate, length / (double)SampleRate, ownedEnd, last);
    }
}
