using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The piano-roll rules: how notes are quantized, moved, resized and transposed. Times are seconds from the clip start.</summary>
public static class NoteRules
{
    /// <summary>The shortest a note may be made by a resize, in seconds.</summary>
    public const double MinDuration = 0.02;

    /// <summary>The shortest note a new note may be, in seconds, when the grid is finer.</summary>
    public const double MinNewDuration = 0.05;

    /// <summary>The velocity of a note added without one.</summary>
    public const int DefaultVelocity = 100;

    /// <summary>The lowest pitch.</summary>
    public const int LowestPitch = 0;

    /// <summary>The highest pitch.</summary>
    public const int HighestPitch = 127;

    /// <summary>The length of a new note: one grid step, and never under <see cref="MinNewDuration"/>.</summary>
    public static double NewDuration(double gridStep) => Math.Max(MinNewDuration, gridStep);

    /// <summary>The latest a note may start in a clip of this length, so it still starts inside the clip.</summary>
    public static double LatestStart(double clipLength) => Math.Max(0, clipLength - MinDuration);

    /// <summary>Snaps a note to the grid: its start to the nearest step from 0, its length to the nearest whole step and at least one.</summary>
    /// <remarks>A start the snap would push to the clip end or past it stays on the last step inside the clip.</remarks>
    public static MidiNote Quantize(MidiNote note, double gridStep, double clipLength)
    {
        if (!(gridStep > 0))
        {
            return note;
        }
        // Halves round up, as the old DAW's Math.round did, not to the even step.
        double start = Math.Max(0, Math.Round(note.Start / gridStep, MidpointRounding.AwayFromZero) * gridStep);
        if (start >= clipLength)
        {
            start = Math.Max(0, (Math.Ceiling(clipLength / gridStep) - 1) * gridStep);
        }
        double duration = Math.Max(gridStep, Math.Round(note.Duration / gridStep, MidpointRounding.AwayFromZero) * gridStep);
        return note with { Start = start, Duration = Math.Min(duration, ProjectLimits.MaxSeconds) };
    }

    /// <summary>Moves a note by a time and a number of semitones; it stays from 0 to the latest start of the clip and in the pitch range.</summary>
    public static MidiNote Move(MidiNote note, double seconds, int semitones, double clipLength)
    {
        double start = Math.Clamp(note.Start + seconds, 0, LatestStart(clipLength));
        return note with { Start = start, Pitch = Math.Clamp(note.Pitch + semitones, LowestPitch, HighestPitch) };
    }

    /// <summary>Lengthens or shortens a note by <paramref name="seconds"/>, to at least <see cref="MinDuration"/>.</summary>
    public static MidiNote Resize(MidiNote note, double seconds)
    {
        return note with { Duration = Math.Clamp(note.Duration + seconds, MinDuration, ProjectLimits.MaxSeconds) };
    }

    /// <summary>The note name with its octave, such as "C4" for pitch 60.</summary>
    public static string Name(int pitch)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return $"{names[pitch % 12]}{(pitch / 12) - 1}";
    }
}
