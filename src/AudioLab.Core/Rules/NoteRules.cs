using System.Globalization;
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

    /// <summary>The most notes one command adds; a bigger paste is several commands, which one gesture keeps as one undo step.</summary>
    public const int MaxAddedNotes = 10_000;

    /// <summary>The highest velocity.</summary>
    public const int HighestVelocity = 127;

    /// <summary>The highest pitch.</summary>
    public const int HighestPitch = 127;

    /// <summary>The highest controller number.</summary>
    public const int HighestController = 127;

    /// <summary>The highest controller value.</summary>
    public const int HighestControllerValue = 127;

    /// <summary>Whether a note that long is kept when an edit cuts a note down to it: <see cref="MinDuration"/> or more, within a billionth.</summary>
    public static bool FitsNote(double duration) => duration >= MinDuration - ProjectLimits.Tolerance;

    /// <summary>The length of a new note: one grid step, and never under <see cref="MinNewDuration"/>.</summary>
    public static double NewDuration(double gridStep) => Math.Max(MinNewDuration, gridStep);

    /// <summary>Whether a note starting at <paramref name="start"/> starts inside a clip of this length, within a billionth of a second.</summary>
    /// <remarks>
    /// A note that ends where its clip ends has a start of <c>(start + duration) - duration</c> in floating point, which can be a little
    /// under the start itself, so the exact comparison would refuse a clip made of the file's own times.
    /// </remarks>
    public static bool StartsInside(double start, double clipLength) => start <= LatestStart(clipLength) + ProjectLimits.Tolerance;

    /// <summary>The latest a note may start in a clip of this length, a little before the end, for add, move and quantize alike.</summary>
    public static double LatestStart(double clipLength) => Math.Max(0, clipLength - MinDuration);

    /// <summary>Snaps a note to the grid: its start to the nearest step from 0, its length to the nearest whole step and at least one.</summary>
    /// <remarks>A start the snap would push past <see cref="LatestStart"/> steps back to the last step inside it.</remarks>
    public static MidiNote Quantize(MidiNote note, double gridStep, double clipLength)
    {
        if (!(gridStep > 0))
        {
            return note;
        }
        // Halves round up, as the old DAW's Math.round did, not to the even step.
        double start = Math.Max(0, Math.Round(note.Start / gridStep, MidpointRounding.AwayFromZero) * gridStep);
        double latest = LatestStart(clipLength);
        while (start > latest && start > 0)
        {
            start = Math.Max(0, start - gridStep);
        }
        double duration = Math.Max(gridStep, Math.Round(note.Duration / gridStep, MidpointRounding.AwayFromZero) * gridStep);
        return note with { Start = Math.Min(start, latest), Duration = Math.Min(duration, ProjectLimits.MaxSeconds) };
    }

    /// <summary>Puts a note at a start and pitch, kept from 0 to <see cref="LatestStart"/> and inside the pitch range.</summary>
    /// <remarks>The target is absolute, so a drag that was clamped and comes back lands exactly on its target.</remarks>
    public static MidiNote MoveTo(MidiNote note, double start, int pitch, double clipLength)
    {
        return note with { Start = Math.Clamp(start, 0, LatestStart(clipLength)), Pitch = Math.Clamp(pitch, LowestPitch, HighestPitch) };
    }

    /// <summary>Gives a note a length, to at least <see cref="MinDuration"/>.</summary>
    public static MidiNote ResizeTo(MidiNote note, double duration)
    {
        return note with { Duration = Math.Clamp(duration, MinDuration, ProjectLimits.MaxSeconds) };
    }

    /// <summary>The note name with its octave, such as "C4" for pitch 60.</summary>
    public static string Name(int pitch)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return string.Create(CultureInfo.InvariantCulture, $"{names[pitch % 12]}{(pitch / 12) - 1}");
    }
}
