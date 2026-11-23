using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Roll;

/// <summary>The edits of the piano roll that a key or a button makes, as commands built from the clip as the page holds it.</summary>
/// <remarks>Where a result is null, there is nothing to send; a refusal the page can see before it sends comes out as a sentence.</remarks>
public static class RollEdits
{
    private const string NoRoom = "There is no room in this clip for the notes there; a note starts inside its clip.";

    /// <summary>Removes notes.</summary>
    public static RollEdit? Delete(MidiClip clip, IReadOnlyCollection<NoteId> notes)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        if (notes.Count == 0)
        {
            return null;
        }
        ClipId id = clip.Id;
        return RollEdit.Chunked([.. notes], (project, part) => new DeleteNotes { Project = project, Clip = id, Notes = part });
    }

    /// <summary>Adds copies of notes, the earliest at <paramref name="at"/> seconds from the clip start; those past the clip are left out.</summary>
    public static RollEdit? Place(MidiClip clip, IReadOnlyList<NewNote> notes, double at, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        refusal = null;
        if (notes.Count == 0)
        {
            return null;
        }
        double latest = NoteRules.LatestStart(clip.Length);
        NewNote[] placed = [.. notes.Select(note => note with { Start = note.Start + at }).Where(note => note.Start <= latest)];
        if (placed.Length == 0)
        {
            refusal = NoRoom;
            return null;
        }
        ClipId id = clip.Id;
        return RollEdit.Chunked(placed, (project, part) => new AddNotes { Project = project, Clip = id, Notes = part }, selectCreated: true);
    }

    /// <summary>Copies notes right after themselves: from the end of the last of them, in time.</summary>
    public static RollEdit? Duplicate(MidiClip clip, IReadOnlyList<MidiNote> notes, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(notes);
        refusal = null;
        if (notes.Count == 0)
        {
            return null;
        }
        double first = notes.Min(note => note.Start);
        double span = notes.Max(note => note.Start + note.Duration) - first;
        NewNote[] copies = [.. notes.Select(note => new NewNote(note.Pitch, note.Start - first, note.Duration, note.Velocity))];
        return Place(clip, copies, first + span, out refusal);
    }

    /// <summary>Snaps notes to the project grid with the server's rule; no notes named means every note.</summary>
    public static RollEdit Quantize(MidiClip clip, IReadOnlyCollection<NoteId> notes)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        ClipId id = clip.Id;
        return notes.Count == 0
            ? RollEdit.Of(project => new QuantizeNotes { Project = project, Clip = id })
            : RollEdit.Chunked([.. notes], (project, part) => new QuantizeNotes { Project = project, Clip = id, Notes = part });
    }

    /// <summary>Transposes notes by semitones; no notes named means every note.</summary>
    public static RollEdit Transpose(MidiClip clip, IReadOnlyCollection<NoteId> notes, int semitones)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        ClipId id = clip.Id;
        return notes.Count == 0
            ? RollEdit.Of(project => new TransposeNotes { Project = project, Clip = id, Semitones = semitones })
            : RollEdit.Chunked(
                [.. notes],
                (project, part) => new TransposeNotes { Project = project, Clip = id, Notes = part, Semitones = semitones });
    }

    /// <summary>Moves notes one grid step: the earliest goes to the next line, the rest keep their places relative to it, inside the clip.</summary>
    public static RollEdit? Nudge(ProjectDocument document, MidiClip clip, IReadOnlyList<MidiNote> notes, int direction)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        if (notes.Count == 0)
        {
            return null;
        }
        GridSnap grid = new(document);
        double first = notes.Min(note => note.Start);
        double wanted = grid.Snap(clip.Start + first + (direction * grid.Step(clip.Start + first))) - clip.Start;
        double latest = NoteRules.LatestStart(clip.Length);
        double delta = Math.Max(-first, Math.Min(wanted - first, latest - notes.Max(note => note.Start)));
        if (Math.Abs(delta) < TimelineRules.NoMove)
        {
            return null;
        }
        NoteTarget[] targets = [.. notes.Select(note => new NoteTarget(note.Id, note.Start + delta, note.Pitch))];
        ClipId id = clip.Id;
        return RollEdit.Chunked(targets, (project, part) => new MoveNotes { Project = project, Clip = id, Notes = part });
    }

    /// <summary>Lengthens or shortens notes by one grid step, together, none below the least length; the server's rule, so no clip end.</summary>
    public static RollEdit? Stretch(ProjectDocument document, MidiClip clip, IReadOnlyList<MidiNote> notes, int direction)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        if (notes.Count == 0)
        {
            return null;
        }
        GridSnap grid = new(document);
        MidiNote lead = notes.OrderBy(note => note.Start).First();
        double end = lead.Start + lead.Duration;
        double wanted = grid.Snap(clip.Start + end + (direction * grid.Step(clip.Start + end))) - clip.Start - end;
        double delta = NoteDrag.FitStretch([.. notes], wanted);
        if (Math.Abs(delta) < TimelineRules.NoMove)
        {
            return null;
        }
        NoteLength[] lengths = [.. notes.Select(note => new NoteLength(note.Id, note.Duration + delta))];
        ClipId id = clip.Id;
        return RollEdit.Chunked(lengths, (project, part) => new ResizeNotes { Project = project, Clip = id, Notes = part });
    }

    /// <summary>Raises or lowers the velocity of notes by an amount, kept from 1 to 127: one command, whatever velocities the notes have.</summary>
    /// <param name="clip">The clip.</param>
    /// <param name="selected">The notes to change; every note of the clip when none are named.</param>
    /// <param name="amount">The amount to add to each velocity.</param>
    public static RollEdit? Louder(MidiClip clip, IReadOnlyCollection<NoteId> selected, int amount)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(selected);
        IEnumerable<MidiNote> touched = selected.Count == 0 ? clip.Notes : clip.Notes.Where(note => selected.Contains(note.Id));
        NoteId[] ids =
        [
            .. touched.Where(note => Math.Clamp(note.Velocity + amount, 1, NoteRules.HighestVelocity) != note.Velocity)
                .Select(note => note.Id),
        ];
        if (ids.Length == 0)
        {
            return null;
        }
        ClipId id = clip.Id;
        return selected.Count == 0
            ? RollEdit.Of(project => new AdjustNoteVelocity { Project = project, Clip = id, Amount = amount })
            : RollEdit.Chunked(ids, (project, part) => new AdjustNoteVelocity { Project = project, Clip = id, Notes = part, Amount = amount });
    }

    /// <summary>Types a note at the insert point: one grid step long, at the pitch and velocity of the last one.</summary>
    public static RollEdit? Type(ProjectDocument document, MidiClip clip, double at, int pitch, int velocity, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        double start = Math.Max(0, at);
        double step = new GridSnap(document).Step(clip.Start + start);
        double length = Math.Max(NoteRules.MinDuration, Math.Min(step, clip.Length - start));
        return Place(clip, [new NewNote(pitch, 0, length, velocity)], start, out refusal);
    }

    /// <summary>Puts the sustain pedal down or up at a time: down when it is up there and up when it is down.</summary>
    /// <remarks>An event already at that time is replaced, in the same gesture, so a time never holds two events of the pedal.</remarks>
    public static RollEdit TogglePedal(MidiClip clip, double time)
    {
        ArgumentNullException.ThrowIfNull(clip);
        double at = Math.Clamp(time, 0, clip.Length);
        int value = PedalDownAt(clip, at) ? 0 : 127;
        ClipId id = clip.Id;
        MidiControlChange? existing = null;
        foreach (MidiControlChange change in clip.ControlChanges)
        {
            if (change.Controller == PianoRollState.PedalController && Math.Abs(change.Time - at) < TimelineRules.NoMove)
            {
                existing = change;
            }
        }
        return new RollEdit(project =>
        {
            List<Command> commands = [];
            if (existing is { } held)
            {
                commands.Add(
                    new RemoveControlChanges { Project = project, Clip = id, Time = held.Time, Controller = PianoRollState.PedalController });
            }
            commands.Add(
                new AddControlChange { Project = project, Clip = id, Time = at, Controller = PianoRollState.PedalController, Value = value });
            return commands;
        });
    }

    /// <summary>Removes the pedal event at a time.</summary>
    public static RollEdit RemovePedal(MidiClip clip, double time)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ClipId id = clip.Id;
        return RollEdit.Of(
            project => new RemoveControlChanges { Project = project, Clip = id, Time = time, Controller = PianoRollState.PedalController });
    }

    /// <summary>Whether the pedal is down at a time: the last pedal event at or before it is 64 or more.</summary>
    public static bool PedalDownAt(MidiClip clip, double time)
    {
        ArgumentNullException.ThrowIfNull(clip);
        MidiControlChange? last = null;
        foreach (MidiControlChange change in clip.ControlChanges)
        {
            if (change.Controller == PianoRollState.PedalController && change.Time <= time + TimelineRules.NoMove)
            {
                last = change;
            }
        }
        return last is { Value: >= 64 };
    }
}
