namespace AudioLab.Audio;

/// <summary>Counts the times the output ran out of audio, as reported by whatever drains the engine.</summary>
public sealed class DropoutCounter
{
    /// <summary>Runs of consecutive output blocks that had no audio.</summary>
    public long Events { get; private set; }

    /// <summary>Output frames that were played as silence.</summary>
    public long Frames { get; private set; }

    /// <summary>Records a dropout of the given length; <paramref name="startsNewEvent" /> is false while a run continues.</summary>
    public void Report(int missingFrames, bool startsNewEvent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(missingFrames);
        Frames += missingFrames;
        if (startsNewEvent)
        {
            Events++;
        }
    }
}
