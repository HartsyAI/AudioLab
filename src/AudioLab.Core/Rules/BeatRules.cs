using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The drum machine's rules from the old DAW: sixteenth-note steps, a swing that pushes odd steps late, pads named after their sample.</summary>
public static class BeatRules
{
    /// <summary>How many steps a new project's pattern has.</summary>
    public const int DefaultSteps = 16;

    /// <summary>The gain of a new lane: unity.</summary>
    public const double DefaultGain = 1;

    /// <summary>How many steps make one quarter note.</summary>
    public const int StepsPerQuarter = 4;

    /// <summary>The longest lane name the old DAW took from a clip or a file name.</summary>
    public const int PadNameLength = 18;

    /// <summary>One step in seconds at a tempo: a sixteenth note. The tempo is the one where the pattern lands, not the playhead's.</summary>
    public static double StepSeconds(double bpm)
    {
        return 60 / Math.Clamp(bpm, ProjectLimits.MinBpm, ProjectLimits.MaxBpm) / StepsPerQuarter;
    }

    /// <summary>Where a step sounds, in seconds from the pattern start: its place on the grid, and every odd step pushed late by the swing.</summary>
    public static double StepStart(int step, double stepSeconds, double swing)
    {
        return (step * stepSeconds) + (step % 2 == 1 ? stepSeconds * swing : 0);
    }

    /// <summary>How long one pass of the pattern lasts, in seconds.</summary>
    public static double PatternSeconds(int steps, double stepSeconds) => steps * stepSeconds;

    /// <summary>The name a pad takes from the clip, file or prompt it came from: trimmed and cut to <see cref="PadNameLength"/> characters.</summary>
    public static string PadName(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string trimmed = source.Trim();
        return trimmed.Length > PadNameLength ? trimmed[..PadNameLength] : trimmed;
    }

    /// <summary>A lane's steps at a new length: the leading steps stay, new ones are off.</summary>
    public static ValueList<bool> Resize(ValueList<bool> steps, int count)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return [.. Enumerable.Range(0, count).Select(i => i < steps.Count && steps[i])];
    }
}
