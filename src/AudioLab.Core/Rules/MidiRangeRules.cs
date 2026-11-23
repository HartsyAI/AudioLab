using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>What deleting a time range does to a MIDI clip and its notes, from the old DAW's range delete. Times are seconds.</summary>
/// <remarks>
/// A clip wholly inside the range is gone. In any other clip the notes that start inside the range and its controller events are removed;
/// a note sounding across the range start stops there; a note that goes on past the range end keeps that part as a new note (a new id,
/// starting at the range end and held to the clip end); and a sustain pedal that is down at the range start is released there, and
/// pressed again at the range end if it is down there. With ripple (shuffle edit mode) later clips move earlier by the length of the
/// range, a clip across the whole range gets that much shorter, one that starts inside it begins at the range start with only the
/// part after the range, and the notes and events after the range move earlier with it. Without ripple the clip keeps its place
/// and length, and the gap stays.
/// </remarks>
public static class MidiRangeRules
{
    /// <summary>The controller number of the sustain pedal.</summary>
    public const int SustainController = 64;

    /// <summary>The pedal is down at this value or above.</summary>
    public const int PedalDown = 64;

    /// <summary>A note must go on past the range end by more than this many seconds to keep a part after it.</summary>
    public const double TailTolerance = 1e-6;

    /// <summary>The clip with the range removed, or null when the clip is wholly inside it and goes.</summary>
    /// <remarks>
    /// The new notes take ids from the clip's <see cref="MidiClip.NextNoteId"/>; the caller has checked that <see cref="NewNotes"/> of them
    /// fit the id space. A clip the range does not touch comes back as it was, apart from the move with ripple.
    /// </remarks>
    public static MidiClip? Delete(MidiClip clip, double start, double end, bool ripple)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (RangeRules.Inside(clip, start, end))
        {
            return null;
        }
        double cs = clip.Start;
        double clipEnd = ClipRules.EndOf(clip);
        (bool pedalAtStart, int? pedalAtEnd) = PedalStates(clip, start, end);
        NoteId next = clip.NextNoteId;
        List<MidiNote> tails = [];
        foreach (MidiNote note in clip.Notes)
        {
            if (TailOf(note, clip, end) is { } tail)
            {
                tails.Add(tail with { Id = next });
                next = next.Next();
            }
        }
        List<MidiNote> notes =
        [
            .. clip.Notes
                .Where(note => cs + note.Start < start || cs + note.Start >= end)
                .Select(note => StopAt(note, cs, start)),
            .. tails,
        ];
        List<MidiControlChange> events = [.. clip.ControlChanges.Where(change => cs + change.Time < start || cs + change.Time >= end)];
        if (pedalAtStart && cs < start && clipEnd > start)
        {
            events.Add(new MidiControlChange(start - cs, SustainController, 0));
        }
        double endTime = end - cs;
        bool pressedAtEnd = events.Any(change => change.Controller == SustainController && Math.Abs(change.Time - endTime) < RangeRules.Tolerance);
        if (pedalAtEnd is { } value && value >= PedalDown && !pressedAtEnd && clipEnd > end + RangeRules.Tolerance)
        {
            events.Add(new MidiControlChange(endTime, SustainController, value));
        }
        MidiClip cut = clip with
        {
            Notes = [.. notes],
            ControlChanges = [.. events.OrderBy(change => change.Time)],
            NextNoteId = next,
        };
        return ripple ? Close(cut, start, end) : cut;
    }

    /// <summary>How many new notes deleting the range from the clip makes: the notes that go on past the range end.</summary>
    public static int NewNotes(MidiClip clip, double start, double end)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return RangeRules.Inside(clip, start, end) ? 0 : clip.Notes.Count(note => TailOf(note, clip, end) is not null);
    }

    // Whether the pedal is down where the range starts, and its last value before the range ends, both read before any event is removed.
    private static (bool AtStart, int? AtEnd) PedalStates(MidiClip clip, double start, double end)
    {
        bool atStart = false;
        int? atEnd = null;
        foreach (MidiControlChange change in clip.ControlChanges.Where(change => change.Controller == SustainController))
        {
            if (clip.Start + change.Time < start)
            {
                atStart = change.Value >= PedalDown;
            }
            if (clip.Start + change.Time < end)
            {
                atEnd = change.Value;
            }
        }
        return (atStart, atEnd);
    }

    // The part of a note after the range end, from the range end to the end of the note or the clip, whichever is first.
    private static MidiNote? TailOf(MidiNote note, MidiClip clip, double end)
    {
        double begin = clip.Start + note.Start;
        double noteEnd = Math.Min(begin + note.Duration, ClipRules.EndOf(clip));
        return begin < end && noteEnd > end + TailTolerance ? note with { Start = end - clip.Start, Duration = noteEnd - end } : null;
    }

    private static MidiNote StopAt(MidiNote note, double clipStart, double start)
    {
        double begin = clipStart + note.Start;
        return begin < start && begin + note.Duration > start ? note with { Duration = start - begin } : note;
    }

    // Shuffle: the clip closes the gap, as the clips of an audio track do.
    private static MidiClip Close(MidiClip clip, double start, double end)
    {
        double gap = end - start;
        double cs = clip.Start;
        double clipEnd = ClipRules.EndOf(clip);
        if (cs >= end - RangeRules.Tolerance)
        {
            return clip with { Start = Math.Max(0, cs - gap) };
        }
        if (cs >= start)
        {
            // It starts inside the range: only the part after the range stays, moved to the range start.
            double cutAway = end - cs;
            return clip with
            {
                Start = start,
                Length = Math.Max(MidiClip.MinLength, clip.Length - cutAway),
                Notes = [.. clip.Notes.Select(note => note with { Start = Math.Max(0, note.Start - cutAway) })],
                ControlChanges = [.. clip.ControlChanges.Select(change => change with { Time = Math.Max(0, change.Time - cutAway) })],
            };
        }
        double length = clipEnd > start ? Math.Max(MidiClip.MinLength, (clipEnd >= end ? clipEnd - gap : start) - cs) : clip.Length;
        bool After(double time) => cs + time >= end - RangeRules.Tolerance;
        return clip with
        {
            Length = length,
            Notes = [.. clip.Notes.Select(note => After(note.Start) ? note with { Start = Math.Max(0, note.Start - gap) } : note)],
            ControlChanges = [.. clip.ControlChanges.Select(change => After(change.Time) ? change with { Time = Math.Max(0, change.Time - gap) } : change)],
        };
    }
}
