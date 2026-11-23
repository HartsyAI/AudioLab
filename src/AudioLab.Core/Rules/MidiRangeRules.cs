using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>What deleting a time range does to a MIDI clip and its notes, from the old DAW's range delete. Times are seconds.</summary>
/// <remarks>
/// A clip wholly inside the range is gone, and one the range does not touch comes back as it was (apart from the move with ripple).
/// In any other clip the notes that start inside the range and its controller events are removed; a note sounding across the range start
/// stops there; a note that goes on past the range end keeps that part as a new note (a new id, starting at the range end and held to the
/// clip end); and a sustain pedal that is down at the range start is released there, and one that is down at the range end is pressed
/// there. A head or a tail shorter than <see cref="NoteRules.MinDuration"/> is dropped, which the old DAW did not do. With ripple
/// (shuffle edit mode) later clips move earlier by the length of the range, a clip across the whole range gets that much shorter, one
/// that starts inside it begins at the range start with only the part after the range, and the notes and events after the range move
/// earlier with it; the release and the press then share an instant, the release first. Without ripple the clip keeps its place
/// and length, and the gap stays. Every comparison with an edge allows <see cref="RangeRules.Tolerance"/>, so a time that is an edge up
/// to rounding is on it.
/// </remarks>
public static class MidiRangeRules
{
    /// <summary>The controller number of the sustain pedal.</summary>
    public const int SustainController = 64;

    /// <summary>The pedal is down at this value or above.</summary>
    public const int PedalDown = 64;

    /// <summary>The clip with the range removed, or null in <paramref name="result"/> when the clip is wholly inside it and goes.</summary>
    /// <returns>False when the result would hold more notes or controller events than a clip may, or use more note ids than exist.</returns>
    /// <remarks>The new notes take ids from the clip's <see cref="MidiClip.NextNoteId"/>. Nothing is changed when this is false.</remarks>
    public static bool TryDelete(MidiClip clip, double start, double end, bool ripple, out MidiClip? result)
    {
        ArgumentNullException.ThrowIfNull(clip);
        result = clip;
        double clipEnd = ClipRules.EndOf(clip);
        if (RangeRules.Inside(clip, start, end))
        {
            result = null;
            return true;
        }
        if (clipEnd <= start + RangeRules.Tolerance)
        {
            return true;
        }
        if (clip.Start >= end - RangeRules.Tolerance)
        {
            result = ripple ? clip with { Start = Math.Max(0, clip.Start - (end - start)) } : clip;
            return true;
        }
        List<MidiNote> tails = [.. clip.Notes.Select(note => TailOf(note, clip, end)).OfType<MidiNote>()];
        if (clip.NextNoteId.Value + (long)tails.Count > NoteId.Max)
        {
            result = null;
            return false;
        }
        NoteId next = clip.NextNoteId;
        List<MidiNote> minted = [];
        foreach (MidiNote tail in tails)
        {
            minted.Add(tail with { Id = next });
            next = next.Next();
        }
        List<MidiNote> notes =
        [
            .. clip.Notes
                .Where(note => !IsInside(clip.Start + note.Start, start, end))
                .Select(note => StopAt(note, clip.Start, start))
                .OfType<MidiNote>(),
            .. minted,
        ];
        List<MidiControlChange> events = Events(clip, start, end, clipEnd);
        if (notes.Count > ProjectLimits.MaxListItems || events.Count > ProjectLimits.MaxListItems)
        {
            result = null;
            return false;
        }
        MidiClip cut = clip with { Notes = [.. notes], ControlChanges = [.. events], NextNoteId = next };
        result = ripple ? Close(cut, start, end) : cut;
        return true;
    }

    // The events outside the range, the release at its start for a pedal down there, the press at its end for a pedal down there, in time order.
    private static List<MidiControlChange> Events(MidiClip clip, double start, double end, double clipEnd)
    {
        bool downAtStart = false;
        int? atEnd = null;
        foreach (MidiControlChange change in clip.ControlChanges.Where(change => change.Controller == SustainController))
        {
            double at = clip.Start + change.Time;
            downAtStart = at < start - RangeRules.Tolerance ? change.Value >= PedalDown : downAtStart;
            atEnd = at < end - RangeRules.Tolerance ? change.Value : atEnd;
        }
        List<MidiControlChange> events = [.. clip.ControlChanges.Where(change => !IsInside(clip.Start + change.Time, start, end))];
        if (downAtStart && clip.Start < start - RangeRules.Tolerance)
        {
            events.Add(new MidiControlChange(start - clip.Start, SustainController, 0));
        }
        double pressAt = end - clip.Start;
        bool pressed = events.Any(change => change.Controller == SustainController && Math.Abs(change.Time - pressAt) < RangeRules.Tolerance);
        if (atEnd is { } value && value >= PedalDown && !pressed && clipEnd > end + RangeRules.Tolerance)
        {
            events.Add(new MidiControlChange(pressAt, SustainController, value));
        }
        return [.. events.OrderBy(change => change.Time)];
    }

    // Whether a time is inside the range [start, end), an edge counting when it is within the tolerance of it.
    private static bool IsInside(double time, double start, double end) => time >= start - RangeRules.Tolerance && time < end - RangeRules.Tolerance;

    // The part of a note after the range end, from the range end to the end of the note or the clip, whichever is first; null when it is
    // too short to be a note.
    private static MidiNote? TailOf(MidiNote note, MidiClip clip, double end)
    {
        double begin = clip.Start + note.Start;
        double noteEnd = Math.Min(begin + note.Duration, ClipRules.EndOf(clip));
        bool crosses = begin < end - RangeRules.Tolerance && NoteRules.FitsNote(noteEnd - end);
        return crosses ? note with { Start = end - clip.Start, Duration = noteEnd - end } : null;
    }

    // A note sounding across the range start stops there; null when what is left of it is too short to be a note.
    private static MidiNote? StopAt(MidiNote note, double clipStart, double start)
    {
        double begin = clipStart + note.Start;
        if (begin < start - RangeRules.Tolerance && begin + note.Duration > start + RangeRules.Tolerance)
        {
            return NoteRules.FitsNote(start - begin) ? note with { Duration = start - begin } : null;
        }
        return note;
    }

    // Shuffle: the clip closes the gap, as the clips of an audio track do.
    private static MidiClip Close(MidiClip clip, double start, double end)
    {
        double gap = end - start;
        double clipStart = clip.Start;
        double clipEnd = ClipRules.EndOf(clip);
        if (clipStart >= start - RangeRules.Tolerance)
        {
            // It starts inside the range: only the part after the range stays, moved to the range start.
            double cutAway = end - clipStart;
            return clip with
            {
                Start = start,
                Length = Math.Max(MidiClip.MinLength, clip.Length - cutAway),
                Notes = [.. clip.Notes.Select(note => note with { Start = Math.Max(0, note.Start - cutAway) })],
                ControlChanges = [.. clip.ControlChanges.Select(change => change with { Time = Math.Max(0, change.Time - cutAway) })],
            };
        }
        double length = (clipEnd >= end - RangeRules.Tolerance ? clipEnd - gap : start) - clipStart;
        double Earlier(double time) => clipStart + time >= end - RangeRules.Tolerance ? Math.Max(0, time - gap) : time;
        return clip with
        {
            Length = Math.Max(MidiClip.MinLength, length),
            Notes = [.. clip.Notes.Select(note => note with { Start = Earlier(note.Start) })],
            ControlChanges = [.. clip.ControlChanges.Select(change => change with { Time = Earlier(change.Time) })],
        };
    }
}
