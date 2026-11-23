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
            .Add("midi_clip_import", "Adds a MIDI clip with its notes and events in one step.", json.ImportMidiClip, ImportClip)
            .Add("midi_note_add", "Adds a note to a MIDI clip and allocates its note id.", json.AddNote, AddNote)
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

    private static CommandResult ImportClip(ProjectDocument document, ImportMidiClip command, IIdService ids)
    {
        int index = TrackIndexOf(document, command.Track);
        if (index < 0)
        {
            return CommandChecks.NotFound("track", command.Track.Value);
        }
        Track track = document.Tracks[index];
        if (track.Kind != TrackKind.Instrument)
        {
            return CommandChecks.Rejected("A MIDI clip goes on an instrument track.");
        }
        CommandResult? invalid = CommandChecks.Finite((nameof(command.Start), command.Start), (nameof(command.Length), command.Length))
            ?? CommandChecks.Range(nameof(command.Start), command.Start, 0, ProjectLimits.MaxSeconds)
            ?? CommandChecks.Range(nameof(command.Length), command.Length, MidiClip.MinLength, ProjectLimits.MaxSeconds)
            ?? CommandChecks.TooLong(command.Name)
            ?? ImportContentProblem(command);
        if (invalid is not null)
        {
            return invalid;
        }
        if (track.MidiClips.Count >= ProjectLimits.MaxClips)
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} MIDI clips.");
        }
        MidiNote[] notes = new MidiNote[command.Notes.Count];
        for (int i = 0; i < notes.Length; i++)
        {
            ImportedNote note = command.Notes[i];
            notes[i] = new MidiNote(new NoteId(i + 1), note.Pitch, note.Start, note.Duration, note.Velocity);
        }
        MidiControlChange[] controls = new MidiControlChange[command.ControlChanges.Count];
        for (int i = 0; i < controls.Length; i++)
        {
            ImportedControlChange control = command.ControlChanges[i];
            controls[i] = new MidiControlChange(control.Time, control.Controller, control.Value);
        }
        MidiClip clip = new()
        {
            Id = ids.Next<ClipId>(),
            Name = string.IsNullOrWhiteSpace(command.Name) ? ClipRules.NextName(document, "MIDI") : command.Name.Trim(),
            Start = command.Start,
            Length = command.Length,
            NextNoteId = new NoteId(notes.Length + 1),
            Notes = new ValueList<MidiNote>(notes),
            ControlChanges = new ValueList<MidiControlChange>(controls),
            Muted = false,
            Color = command.Color,
        };
        ProjectDocument edited = ClipEdits.WithTrack(document, index, current => current with { MidiClips = [.. current.MidiClips, clip] });
        return CommandResult.Ok(edited, clip.Id.Value);
    }

    // The notes and events of an import, each held to the rules of the command that adds one; the first problem names its place.
    private static CommandResult? ImportContentProblem(ImportMidiClip command)
    {
        if (command.Notes.Count > ProjectLimits.MaxListItems || command.ControlChanges.Count > ProjectLimits.MaxListItems)
        {
            return CommandChecks.OutOfRange($"A clip holds at most {ProjectLimits.MaxListItems} notes and as many controller events.");
        }
        double latest = NoteRules.LatestStart(command.Length);
        for (int i = 0; i < command.Notes.Count; i++)
        {
            ImportedNote note = command.Notes[i];
            bool fits = note.Pitch is >= NoteRules.LowestPitch and <= NoteRules.HighestPitch
                && note.Velocity is >= 1 and <= NoteRules.HighestPitch
                && double.IsFinite(note.Start) && note.Start >= 0 && NoteRules.StartsInside(note.Start, command.Length)
                && double.IsFinite(note.Duration) && note.Duration >= NoteRules.MinDuration && note.Duration <= ProjectLimits.MaxSeconds;
            if (!fits)
            {
                return CommandChecks.OutOfRange(
                    $"Note {i + 1} is not a note of this clip: a pitch from 0 to 127, a velocity from 1 to 127, a start from 0 to "
                    + $"{latest.ToString(CultureInfo.InvariantCulture)} seconds and a duration of at least {NoteRules.MinDuration} seconds.");
            }
        }
        double previous = 0;
        HashSet<(double, int)> seen = [];
        for (int i = 0; i < command.ControlChanges.Count; i++)
        {
            ImportedControlChange control = command.ControlChanges[i];
            bool fits = control.Controller is >= 0 and <= NoteRules.HighestController
                && control.Value is >= 0 and <= NoteRules.HighestControllerValue
                && double.IsFinite(control.Time) && control.Time >= 0 && control.Time <= command.Length;
            if (!fits)
            {
                return CommandChecks.OutOfRange($"Controller event {i + 1} is outside the clip, or has a controller or value outside 0 to 127.");
            }
            if (control.Time < previous)
            {
                return CommandChecks.OutOfRange($"Controller event {i + 1} comes before the one that precedes it; the events are in time order.");
            }
            if (!seen.Add((control.Time, control.Controller)))
            {
                return CommandChecks.OutOfRange($"Controller event {i + 1} repeats the controller and time of an earlier one.");
            }
            previous = control.Time;
        }
        return null;
    }

    private static int TrackIndexOf(ProjectDocument document, TrackId id)
    {
        for (int index = 0; index < document.Tracks.Count; index++)
        {
            if (document.Tracks[index].Id == id)
            {
                return index;
            }
        }
        return -1;
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
        if (invalid is null && command.Start > NoteRules.LatestStart(clip.Length))
        {
            string latest = NoteRules.LatestStart(clip.Length).ToString(CultureInfo.InvariantCulture);
            invalid = CommandChecks.OutOfRange($"A note starts inside its clip, at {latest} seconds at the latest.");
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
        double duration = command.Duration ?? NoteRules.NewDuration(GridStep(document, clip));
        MidiNote note = new(clip.NextNoteId, command.Pitch, command.Start, duration, command.Velocity ?? NoteRules.DefaultVelocity);
        MidiClip edited = clip with { Notes = [.. clip.Notes, note], NextNoteId = clip.NextNoteId.Next() };
        return CommandResult.Ok(Replace(document, at, edited), note.Id.Value.ToString(CultureInfo.InvariantCulture));
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
            () => CommandChecks.Range(nameof(command.Velocity), command.Velocity, 1, NoteRules.HighestPitch),
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
