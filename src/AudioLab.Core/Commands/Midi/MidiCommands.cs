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
            .Add("midi_notes_move", "Moves notes in time and pitch.", json.MoveNotes, MoveNotes)
            .Add("midi_notes_resize", "Makes notes longer or shorter.", json.ResizeNotes, ResizeNotes)
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
        if (Find(document, command.Clip, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        CommandResult? invalid = CommandChecks.Range(nameof(command.Pitch), command.Pitch, NoteRules.LowestPitch, NoteRules.HighestPitch)
            ?? CommandChecks.Range(nameof(command.Start), command.Start, 0, ProjectLimits.MaxSeconds);
        if (invalid is null && command.Start >= clip.Length)
        {
            invalid = CommandChecks.OutOfRange($"A note starts inside its clip, before {clip.Length.ToString(CultureInfo.InvariantCulture)} seconds.");
        }
        invalid ??= CommandChecks.Range(nameof(command.Duration), command.Duration, NoteRules.MinDuration, ProjectLimits.MaxSeconds)
            ?? CommandChecks.Range(nameof(command.Velocity), command.Velocity, 1, NoteRules.HighestPitch);
        if (invalid is not null)
        {
            return invalid;
        }
        if (clip.Notes.Count >= ProjectLimits.MaxListItems || clip.NextNoteId.Value >= NoteId.Max)
        {
            return CommandChecks.OutOfRange("The clip holds as many notes as a clip may, or has used every note id.");
        }
        double step = GridStep(document, clip);
        MidiNote note = new(clip.NextNoteId, command.Pitch, command.Start, command.Duration ?? NoteRules.NewDuration(step), command.Velocity ?? NoteRules.DefaultVelocity);
        ProjectDocument edited = Replace(document, at, clip with { Notes = [.. clip.Notes, note], NextNoteId = clip.NextNoteId.Next() });
        return CommandResult.Ok(edited, note.Id.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static CommandResult MoveNotes(ProjectDocument document, MoveNotes command, IIdService ids)
    {
        return EditNotes(document, command.Clip, command.Notes, () => CommandChecks.Range(nameof(command.Time), command.Time, -ProjectLimits.MaxSeconds, ProjectLimits.MaxSeconds)
                ?? CommandChecks.Range(nameof(command.Pitch), command.Pitch, -NoteRules.HighestPitch, NoteRules.HighestPitch),
            (clip, note) => NoteRules.Move(note, command.Time, command.Pitch, clip.Length));
    }

    private static CommandResult ResizeNotes(ProjectDocument document, ResizeNotes command, IIdService ids)
    {
        return EditNotes(document, command.Clip, command.Notes, () => CommandChecks.Range(nameof(command.Duration), command.Duration, -ProjectLimits.MaxSeconds, ProjectLimits.MaxSeconds),
            (_, note) => NoteRules.Resize(note, command.Duration));
    }

    private static CommandResult SetVelocity(ProjectDocument document, SetNoteVelocity command, IIdService ids)
    {
        return EditNotes(document, command.Clip, command.Notes, () => CommandChecks.Range(nameof(command.Velocity), command.Velocity, 1, NoteRules.HighestPitch),
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
        if (command.Notes.Count == 0 || command.Notes.Count > ProjectLimits.MaxListItems)
        {
            return CommandChecks.OutOfRange($"Name from 1 to {ProjectLimits.MaxListItems} notes.");
        }
        HashSet<NoteId> held = [.. clip.Notes.Select(note => note.Id)];
        NoteId unknown = command.Notes.FirstOrDefault(id => !held.Contains(id));
        if (unknown.Value != 0)
        {
            return CommandChecks.NotFound("note", unknown.ToString());
        }
        HashSet<NoteId> gone = [.. command.Notes];
        return CommandResult.Ok(Replace(document, at, clip with { Notes = [.. clip.Notes.Where(note => !gone.Contains(note.Id))] }));
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
        int index = events.FindLastIndex(existing => existing.Time <= added.Time) + 1;
        events.Insert(index, added);
        return CommandResult.Ok(Replace(document, at, clip with { ControlChanges = new ValueList<MidiControlChange>(events) }));
    }

    private static CommandResult RemoveControl(ProjectDocument document, RemoveControlChanges command, IIdService ids)
    {
        if (Find(document, command.Clip, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        if (CommandChecks.Range(nameof(command.Time), command.Time, 0, ProjectLimits.MaxSeconds) is { } refusal)
        {
            return refusal;
        }
        bool Matches(MidiControlChange change) => change.Controller == command.Controller && Math.Abs(change.Time - command.Time) < TimelineRules.NoMove;
        if (!clip.ControlChanges.Any(Matches))
        {
            return CommandChecks.NotFound("controller event", command.Controller.ToString(CultureInfo.InvariantCulture));
        }
        return CommandResult.Ok(Replace(document, at, clip with { ControlChanges = [.. clip.ControlChanges.Where(change => !Matches(change))] }));
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
        ValueList<MidiControlChange> kept = command.Controller is { } only ? [.. clip.ControlChanges.Where(change => change.Controller != only)] : [];
        return CommandResult.Ok(Replace(document, at, clip with { ControlChanges = kept }));
    }

    // The clip is found and the notes chosen (all of them when none are named); the check runs after the lookups, as elsewhere.
    private static CommandResult EditNotes(
        ProjectDocument document,
        ClipId clipId,
        ValueList<NoteId>? chosen,
        Func<CommandResult?> check,
        Func<MidiClip, MidiNote, MidiNote> edit)
    {
        if (Find(document, clipId, out ClipEdits.Location at) is { } missing)
        {
            return missing;
        }
        MidiClip clip = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        if (chosen is not null)
        {
            HashSet<NoteId> held = [.. clip.Notes.Select(note => note.Id)];
            NoteId unknown = chosen.FirstOrDefault(id => !held.Contains(id));
            if (unknown.Value != 0)
            {
                return CommandChecks.NotFound("note", unknown.ToString());
            }
        }
        if (check() is { } refusal)
        {
            return refusal;
        }
        HashSet<NoteId>? picked = chosen is null ? null : [.. chosen];
        MidiClip edited = clip with { Notes = [.. clip.Notes.Select(note => picked is null || picked.Contains(note.Id) ? edit(clip, note) : note)] };
        return CommandResult.Ok(Replace(document, at, edited));
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
