using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The MIDI commands: the notes and controller events of a MIDI clip, edited as the piano roll edits them.</summary>
internal static class MidiCommands
{
    public static void Register(CommandRegistryBuilder builder, MidiCommandJsonContext json)
    {
        builder
            .Add("midi_note_add", "Adds a note to a MIDI clip and allocates its note id.", json.AddNote, AddNote)
            .Add("midi_notes_add", "Adds notes to a MIDI clip in one step and allocates their note ids.", json.AddNotes, AddNotes)
            .Add("midi_notes_move", "Puts notes at new starts and pitches.", json.MoveNotes, MoveNotes)
            .Add("midi_notes_resize", "Gives notes new lengths.", json.ResizeNotes, ResizeNotes)
            .Add("midi_notes_set_velocity", "Sets the velocity of notes.", json.SetNoteVelocity, SetVelocity)
            .Add("midi_notes_transpose", "Transposes notes by semitones.", json.TransposeNotes, Transpose)
            .Add("midi_notes_quantize", "Snaps notes to the project grid.", json.QuantizeNotes, Quantize)
            .Add("midi_notes_delete", "Removes notes from a MIDI clip.", json.DeleteNotes, DeleteNotes)
            .Add("midi_control_add", "Adds a controller event to a MIDI clip.", json.AddControlChange, AddControl)
            .Add("midi_control_remove", "Removes the events of one controller at one time.", json.RemoveControlChanges, RemoveControl)
            .Add("midi_control_clear", "Removes the controller events of a MIDI clip.", json.ClearControlChanges, ClearControl);
    }

    private static CommandResult AddNote(ProjectDocument document, AddNote command, IIdService ids)
    {
        return AddAll(document, command.Clip, [new NewNote(command.Pitch, command.Start, command.Duration, command.Velocity)]);
    }

    private static CommandResult AddNotes(ProjectDocument document, AddNotes command, IIdService ids)
    {
        return AddAll(document, command.Clip, command.Notes, listed: true);
    }

    // One rule for one note and many: the first note that is refused refuses them all, and the ids count up in list order. A refusal from a list
    // names the note by its place in it, from 0; the one note of midi_note_add is not named.
    private static CommandResult AddAll(ProjectDocument document, ClipId clipId, IReadOnlyList<NewNote> added, bool listed = false)
    {
        if (Find(document, clipId, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        if (added.Count == 0 || added.Count > NoteRules.MaxAddedNotes)
        {
            return CommandChecks.OutOfRange($"Add from 1 to {NoteRules.MaxAddedNotes} notes in one command; split a bigger paste into several.");
        }
        double latestStart = NoteRules.LatestStart(clip.Length);
        for (int index = 0; index < added.Count; index++)
        {
            NewNote note = added[index];
            CommandResult? invalid = CommandChecks.Range(nameof(note.Pitch), note.Pitch, NoteRules.LowestPitch, NoteRules.HighestPitch)
                ?? CommandChecks.Range(nameof(note.Start), note.Start, 0, ProjectLimits.MaxSeconds);
            if (invalid is null && note.Start > latestStart)
            {
                string latest = latestStart.ToString(CultureInfo.InvariantCulture);
                invalid = CommandChecks.OutOfRange($"A note starts inside its clip, at {latest} seconds at the latest.");
            }
            invalid ??= CommandChecks.Range(nameof(note.Duration), note.Duration, NoteRules.MinDuration, ProjectLimits.MaxSeconds)
                ?? CommandChecks.Range(nameof(note.Velocity), note.Velocity, 1, NoteRules.HighestVelocity);
            if (invalid is not null)
            {
                return listed ? CommandResult.Refuse(invalid.Errors[0].Code, $"Note {index}: {invalid.Errors[0].Message}") : invalid;
            }
        }
        if (added.Count > ProjectLimits.MaxListItems - clip.Notes.Count || added.Count > NoteId.Max - clip.NextNoteId.Value)
        {
            return CommandChecks.OutOfRange("The clip holds as many notes as a clip may, or has used every note id.");
        }
        double step = NoteRules.NewDuration(GridStep(document, clip));
        List<MidiNote> notes = [.. clip.Notes];
        List<string> created = [];
        NoteId next = clip.NextNoteId;
        foreach (NewNote note in added)
        {
            notes.Add(new MidiNote(next, note.Pitch, note.Start, note.Duration ?? step, note.Velocity ?? NoteRules.DefaultVelocity));
            created.Add(next.Value.ToString(CultureInfo.InvariantCulture));
            next = next.Next();
        }
        MidiClip edited = clip with { Notes = new ValueList<MidiNote>(notes), NextNoteId = next };
        return CommandResult.Ok(Replace(document, at, edited), [.. created]);
    }

    private static CommandResult MoveNotes(ProjectDocument document, MoveNotes command, IIdService ids)
    {
        Dictionary<NoteId, NoteTarget> targets = [];
        foreach (NoteTarget target in command.Notes)
        {
            targets[target.Note] = target;
        }
        CommandResult? Check()
        {
            foreach (NoteTarget target in command.Notes)
            {
                CommandResult? invalid = CommandChecks.Range("start", target.Start, 0, ProjectLimits.MaxSeconds)
                    ?? CommandChecks.Range("pitch", target.Pitch, NoteRules.LowestPitch, NoteRules.HighestPitch);
                if (invalid is not null)
                {
                    return invalid;
                }
            }
            return null;
        }
        return EditNotes(
            document,
            command.Clip,
            [.. command.Notes.Select(target => target.Note)],
            Check,
            (clip, note) => NoteRules.MoveTo(note, targets[note.Id].Start, targets[note.Id].Pitch, clip.Length));
    }

    private static CommandResult ResizeNotes(ProjectDocument document, ResizeNotes command, IIdService ids)
    {
        Dictionary<NoteId, NoteLength> lengths = [];
        foreach (NoteLength length in command.Notes)
        {
            lengths[length.Note] = length;
        }
        CommandResult? Check()
        {
            foreach (NoteLength length in command.Notes)
            {
                if (CommandChecks.Range("duration", length.Duration, NoteRules.MinDuration, ProjectLimits.MaxSeconds) is { } invalid)
                {
                    return invalid;
                }
            }
            return null;
        }
        return EditNotes(
            document,
            command.Clip,
            [.. command.Notes.Select(length => length.Note)],
            Check,
            (_, note) => NoteRules.ResizeTo(note, lengths[note.Id].Duration));
    }

    private static CommandResult SetVelocity(ProjectDocument document, SetNoteVelocity command, IIdService ids)
    {
        return EditNotes(
            document,
            command.Clip,
            command.Notes,
            () => CommandChecks.Range(nameof(command.Velocity), command.Velocity, 1, NoteRules.HighestVelocity),
            (_, note) => note with { Velocity = command.Velocity });
    }

    private static CommandResult Transpose(ProjectDocument document, TransposeNotes command, IIdService ids)
    {
        return EditNotes(
            document,
            command.Clip,
            command.Notes,
            () => CommandChecks.Range(nameof(command.Semitones), command.Semitones, -NoteRules.HighestPitch, NoteRules.HighestPitch),
            (_, note) => note with { Pitch = Math.Clamp(note.Pitch + command.Semitones, NoteRules.LowestPitch, NoteRules.HighestPitch) });
    }

    private static CommandResult Quantize(ProjectDocument document, QuantizeNotes command, IIdService ids)
    {
        double step = 0;
        if (Find(document, command.Clip, out ClipEdits.Location at) is null)
        {
            step = GridStep(document, document.Tracks[at.Track].MidiClips[at.MidiIndex]);
        }
        return EditNotes(document, command.Clip, command.Notes, () => null, (clip, note) => NoteRules.Quantize(note, step, clip.Length));
    }

    private static CommandResult DeleteNotes(ProjectDocument document, DeleteNotes command, IIdService ids)
    {
        if (Find(document, command.Clip, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        if (Choose(clip, command.Notes, out HashSet<NoteId>? gone) is { } refusal)
        {
            return refusal;
        }
        return CommandResult.Ok(Replace(document, at, clip with { Notes = [.. clip.Notes.Where(note => !gone!.Contains(note.Id))] }));
    }

    private static CommandResult AddControl(ProjectDocument document, AddControlChange command, IIdService ids)
    {
        if (Find(document, command.Clip, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        CommandResult? invalid = CommandChecks.Range(nameof(command.Time), command.Time, 0, clip.Length)
            ?? CommandChecks.Range(nameof(command.Controller), command.Controller, 0, NoteRules.HighestPitch)
            ?? CommandChecks.Range(nameof(command.Value), command.Value, 0, NoteRules.HighestPitch);
        if (invalid is not null)
        {
            return invalid;
        }
        if (clip.ControlChanges.Count >= ProjectLimits.MaxListItems)
        {
            return CommandChecks.OutOfRange("The clip holds as many controller events as a clip may.");
        }
        MidiControlChange added = new(command.Time, command.Controller, command.Value);
        List<MidiControlChange> events = [.. clip.ControlChanges];
        events.Insert(events.FindLastIndex(existing => existing.Time <= added.Time) + 1, added);
        return CommandResult.Ok(Replace(document, at, clip with { ControlChanges = new ValueList<MidiControlChange>(events) }));
    }

    private static CommandResult RemoveControl(ProjectDocument document, RemoveControlChanges command, IIdService ids)
    {
        if (Find(document, command.Clip, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        CommandResult? invalid = CommandChecks.Range(nameof(command.Controller), command.Controller, 0, NoteRules.HighestPitch)
            ?? CommandChecks.Range(nameof(command.Time), command.Time, 0, ProjectLimits.MaxSeconds);
        if (invalid is not null)
        {
            return invalid;
        }
        bool Matches(MidiControlChange change)
        {
            return change.Controller == command.Controller && Math.Abs(change.Time - command.Time) < TimelineRules.NoMove;
        }
        if (!clip.ControlChanges.Any(Matches))
        {
            return CommandChecks.NotFound("controller event", command.Controller.ToString(CultureInfo.InvariantCulture));
        }
        MidiClip edited = clip with { ControlChanges = [.. clip.ControlChanges.Where(change => !Matches(change))] };
        return CommandResult.Ok(Replace(document, at, edited));
    }

    private static CommandResult ClearControl(ProjectDocument document, ClearControlChanges command, IIdService ids)
    {
        if (Find(document, command.Clip, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        if (CommandChecks.Range(nameof(command.Controller), command.Controller, 0, NoteRules.HighestPitch) is { } refusal)
        {
            return refusal;
        }
        ValueList<MidiControlChange> kept = command.Controller is { } only
            ? [.. clip.ControlChanges.Where(change => change.Controller != only)]
            : [];
        return CommandResult.Ok(Replace(document, at, clip with { ControlChanges = kept }));
    }

    // The clip is found and the notes chosen (all of them when none are named); the check runs after the lookups, as elsewhere.
    private static CommandResult EditNotes(
        ProjectDocument document,
        ClipId clipId,
        IReadOnlyCollection<NoteId>? chosen,
        Func<CommandResult?> check,
        Func<MidiClip, MidiNote, MidiNote> edit)
    {
        if (Find(document, clipId, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        CommandResult? refusal = Choose(clip, chosen, out HashSet<NoteId>? picked) ?? check();
        if (refusal is not null)
        {
            return refusal;
        }
        MidiClip edited = clip with { Notes = [.. clip.Notes.Select(note => picked is null || picked.Contains(note.Id) ? edit(clip, note) : note)] };
        return CommandResult.Ok(Replace(document, at, edited));
    }

    // A refusal when the list is empty, too long, or names a note the clip does not hold (a hand-built id of 0 included).
    // No list means every note, and comes out as null.
    private static CommandResult? Choose(MidiClip clip, IReadOnlyCollection<NoteId>? chosen, out HashSet<NoteId>? picked)
    {
        picked = null;
        if (chosen is null)
        {
            return null;
        }
        if (chosen.Count == 0 || chosen.Count > ProjectLimits.MaxListItems)
        {
            return CommandChecks.OutOfRange($"Name from 1 to {ProjectLimits.MaxListItems} notes, or leave the list out for every note.");
        }
        HashSet<NoteId> held = [.. clip.Notes.Select(note => note.Id)];
        foreach (NoteId id in chosen)
        {
            if (!held.Contains(id))
            {
                return CommandChecks.NotFound("note", id.ToString());
            }
        }
        picked = [.. chosen];
        return null;
    }

    // A refusal when the id names no MIDI clip; the place of the clip comes out when it does.
    private static CommandResult? Find(ProjectDocument document, ClipId id, out ClipEdits.Location at)
    {
        at = default;
        if (ClipEdits.Find(document, id) is not { } found)
        {
            return ClipEdits.NoClip(id);
        }
        at = found;
        return found.IsAudio ? ClipEdits.WrongKind(wantsAudio: false) : null;
    }

    private static ProjectDocument Replace(ProjectDocument document, ClipEdits.Location at, MidiClip clip)
    {
        return ClipEdits.WithTrack(document, at.Track, track => track with
        {
            MidiClips = [.. track.MidiClips.Select((candidate, index) => index == at.MidiIndex ? clip : candidate)],
        });
    }

    // The grid step at the tempo where the clip starts, not at the playhead (audit row 38).
    private static double GridStep(ProjectDocument document, MidiClip clip)
    {
        return GridRules.StepSeconds(document.View.Grid, TempoMap.From(document), document.TimeSignature, clip.Start);
    }
}
