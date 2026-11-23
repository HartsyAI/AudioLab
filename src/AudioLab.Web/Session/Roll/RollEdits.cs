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
        ValueList<NoteId> ids = [.. notes];
        ClipId id = clip.Id;
        return RollEdit.Of(project => new DeleteNotes { Project = project, Clip = id, Notes = ids });
    }

    /// <summary>Adds copies of notes at <paramref name="at"/> seconds from the clip start, the earliest of them there; the ones that would start past the clip are left out.</summary>
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
        ValueList<NewNote> placed = [.. notes.Select(note => note with { Start = note.Start + at }).Where(note => note.Start <= latest)];
        if (placed.Count == 0)
        {
            refusal = NoRoom;
            return null;
        }
        ClipId id = clip.Id;
        return RollEdit.Of(project => new AddNotes { Project = project, Clip = id, Notes = placed }, selectCreated: true);
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
        ValueList<NoteId>? ids = notes.Count == 0 ? null : [.. notes];
        ClipId id = clip.Id;
        return RollEdit.Of(project => new QuantizeNotes { Project = project, Clip = id, Notes = ids });
    }

    /// <summary>Transposes notes by semitones; no notes named means every note.</summary>
    public static RollEdit Transpose(MidiClip clip, IReadOnlyCollection<NoteId> notes, int semitones)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        ValueList<NoteId>? ids = notes.Count == 0 ? null : [.. notes];
        ClipId id = clip.Id;
        return RollEdit.Of(project => new TransposeNotes { Project = project, Clip = id, Notes = ids, Semitones = semitones });
    }

    /// <summary>Moves notes one grid step: the earliest goes to the next line and the rest keep their places relative to it, all inside the clip.</summary>
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
        ValueList<NoteTarget> targets = [.. notes.Select(note => new NoteTarget(note.Id, note.Start + delta, note.Pitch))];
        ClipId id = clip.Id;
        return RollEdit.Of(project => new MoveNotes { Project = project, Clip = id, Notes = targets });
    }

    /// <summary>Lengthens or shortens notes by one grid step, the notes together, none below the least length or past the clip.</summary>
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
        double room = Math.Max(0, clip.Length - notes.Max(note => note.Start + note.Duration));
        double delta = Math.Max(NoteRules.MinDuration - notes.Min(note => note.Duration), Math.Min(wanted, room));
        if (Math.Abs(delta) < TimelineRules.NoMove)
        {
            return null;
        }
        ValueList<NoteLength> lengths = [.. notes.Select(note => new NoteLength(note.Id, note.Duration + delta))];
        ClipId id = clip.Id;
        return RollEdit.Of(project => new ResizeNotes { Project = project, Clip = id, Notes = lengths });
    }

    /// <summary>Raises or lowers the velocity of notes by an amount, kept from 1 to 127; notes that land on one value are sent together.</summary>
    public static RollEdit? Louder(MidiClip clip, IReadOnlyList<MidiNote> notes, int amount)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(notes);
        IGrouping<int, MidiNote>[] groups =
        [
            .. notes.Where(note => Math.Clamp(note.Velocity + amount, 1, 127) != note.Velocity)
                .GroupBy(note => Math.Clamp(note.Velocity + amount, 1, 127)),
        ];
        if (groups.Length == 0)
        {
            return null;
        }
        ClipId id = clip.Id;
        (int Velocity, ValueList<NoteId> Notes)[] sets = [.. groups.Select(group => (group.Key, new ValueList<NoteId>([.. group.Select(note => note.Id)])))];
        return new RollEdit(project =>
            [.. sets.Select(set => (Command)new SetNoteVelocity { Project = project, Clip = id, Notes = set.Notes, Velocity = set.Velocity })]);
    }

    /// <summary>Types a note at the insert point: one grid step long, at the pitch and velocity of the last one.</summary>
    public static RollEdit? Type(ProjectDocument document, MidiClip clip, double at, int pitch, int velocity, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clip);
        double start = Math.Max(0, at);
        double length = new GridSnap(document).Step(clip.Start + start);
        return Place(clip, [new NewNote(pitch, 0, Math.Max(NoteRules.MinDuration, Math.Min(length, clip.Length - start)), velocity)], start, out refusal);
    }

    /// <summary>Puts the sustain pedal down or up at a time: down when it is up there and up when it is down.</summary>
    public static RollEdit TogglePedal(MidiClip clip, double time)
    {
        ArgumentNullException.ThrowIfNull(clip);
        int value = PedalDownAt(clip, time) ? 0 : 127;
        ClipId id = clip.Id;
        return RollEdit.Of(project => new AddControlChange { Project = project, Clip = id, Time = Math.Clamp(time, 0, clip.Length), Controller = PianoRollState.PedalController, Value = value });
    }

    /// <summary>Removes the pedal event at a time.</summary>
    public static RollEdit RemovePedal(MidiClip clip, double time)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ClipId id = clip.Id;
        return RollEdit.Of(project => new RemoveControlChanges { Project = project, Clip = id, Time = time, Controller = PianoRollState.PedalController });
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
