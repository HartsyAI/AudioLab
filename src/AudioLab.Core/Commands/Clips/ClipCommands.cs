using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The clip commands: audio and MIDI clips are added, moved, cut, copied and removed, and an audio clip is trimmed and faded.</summary>
internal static class ClipCommands
{
    public static void Register(CommandRegistryBuilder builder, ClipCommandJsonContext json)
    {
        builder
            .Add("clip_add", "Adds an audio clip that plays a media file and allocates the clip id.", json.AddClip, AddAudio)
            .Add("midi_clip_add", "Adds an empty MIDI clip to an instrument track and allocates the clip id.", json.AddMidiClip, AddMidi)
            .Add("clip_move", "Moves an audio or MIDI clip in time, and to another track of its kind.", json.MoveClip, Move)
            .Add("clip_trim", "Moves one edge of an audio clip without changing the audio.", json.TrimClip, Trim)
            .Add("clip_split", "Cuts an audio clip in two and allocates the right piece's id.", json.SplitClip, Split)
            .Add("clip_duplicate", "Copies an audio or MIDI clip onto its track and allocates the copy's id.", json.DuplicateClip, Duplicate)
            .Add("clip_delete", "Removes an audio or MIDI clip; shuffle edit mode closes the gap.", json.DeleteClip, Delete)
            .Add("clip_set_gain", "Sets the gain of an audio clip.", json.SetClipGain, SetGain)
            .Add("clip_set_fades", "Sets the fades of an audio clip.", json.SetClipFades, SetFades)
            .Add("midi_clip_set_length", "Sets the length of a MIDI clip.", json.SetMidiClipLength, SetMidiLength)
            .Add("clip_set_muted", "Mutes or unmutes an audio or MIDI clip.", json.SetClipMuted, SetMuted)
            .Add("clip_rename", "Renames an audio or MIDI clip.", json.RenameClip, Rename)
            .Add("clip_set_color", "Sets a clip's colour, or lets it follow its track.", json.SetClipColor, SetColor)
            .Add("clip_reorder", "Puts a clip over or under the other clips of its track.", json.ReorderClip, Reorder);
    }

    private static CommandResult AddAudio(ProjectDocument document, AddClip command, IIdService ids)
    {
        int index = TrackIndex(document, command.Track);
        if (index < 0)
        {
            return CommandChecks.NotFound("track", command.Track.Value);
        }
        Track track = document.Tracks[index];
        if (track.Kind != TrackKind.Audio)
        {
            return CommandChecks.Rejected("An audio clip goes on an audio track.");
        }
        if (CommandChecks.Finite((nameof(command.Duration), command.Duration), (nameof(command.Start), command.Start)) is { } refusal)
        {
            return refusal;
        }
        if (!(command.Duration >= ProjectLimits.Tolerance) || command.Start < 0)
        {
            return CommandChecks.OutOfRange("A clip has a duration above 0 and starts at the beginning of the project or later.");
        }
        if (CommandChecks.TooLong(command.Name) is { } tooLong)
        {
            return tooLong;
        }
        if (track.Clips.Count >= ProjectLimits.MaxClips)
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} audio clips.");
        }
        AudioClip clip = new()
        {
            Id = ids.Next<ClipId>(),
            Name = string.IsNullOrWhiteSpace(command.Name) ? ClipRules.NextName(document, "Clip") : command.Name.Trim(),
            Media = command.Media,
            Start = command.Start ?? track.Clips.Select(ClipRules.EndOf).DefaultIfEmpty(0).Max(),
            Duration = command.Duration,
            Offset = 0,
            TrimEnd = 0,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
            Color = command.Color,
        };
        return CommandResult.Ok(ClipEdits.WithTrack(document, index, current => current with { Clips = [.. current.Clips, clip] }), clip.Id.Value);
    }

    private static CommandResult AddMidi(ProjectDocument document, AddMidiClip command, IIdService ids)
    {
        int index = TrackIndex(document, command.Track);
        if (index < 0)
        {
            return CommandChecks.NotFound("track", command.Track.Value);
        }
        Track track = document.Tracks[index];
        if (track.Kind != TrackKind.Instrument)
        {
            return CommandChecks.Rejected("A MIDI clip goes on an instrument track.");
        }
        if (CommandChecks.Finite((nameof(command.Start), command.Start), (nameof(command.Length), command.Length)) is { } refusal)
        {
            return refusal;
        }
        if (command.Start < 0 || command.Length < MidiClip.MinLength)
        {
            return CommandChecks.OutOfRange($"A MIDI clip starts at the beginning of the project or later and is at least {MidiClip.MinLength} seconds long.");
        }
        if (CommandChecks.TooLong(command.Name) is { } tooLong)
        {
            return tooLong;
        }
        if (track.MidiClips.Count >= ProjectLimits.MaxClips)
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} MIDI clips.");
        }
        MidiClip clip = new()
        {
            Id = ids.Next<ClipId>(),
            Name = string.IsNullOrWhiteSpace(command.Name) ? ClipRules.NextName(document, "MIDI") : command.Name.Trim(),
            Start = command.Start,
            Length = command.Length,
            NextNoteId = NoteId.First,
            Notes = [],
            ControlChanges = [],
            Muted = false,
            Color = command.Color,
        };
        return CommandResult.Ok(ClipEdits.WithTrack(document, index, current => current with { MidiClips = [.. current.MidiClips, clip] }), clip.Id.Value);
    }

    private static CommandResult Move(ProjectDocument document, MoveClip command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        if (CommandChecks.Range(nameof(command.Start), command.Start, 0, ProjectLimits.MaxSeconds) is { } refusal)
        {
            return refusal;
        }
        double start = command.Start;
        Track source = document.Tracks[at.Track];
        TrackId targetId = command.Track ?? source.Id;
        int target = TrackIndex(document, targetId);
        if (target < 0)
        {
            return CommandChecks.NotFound("track", targetId.Value);
        }
        TrackKind needed = at.IsAudio ? TrackKind.Audio : TrackKind.Instrument;
        if (document.Tracks[target].Kind != needed)
        {
            return CommandChecks.Rejected(at.IsAudio ? "An audio clip goes on an audio track." : "A MIDI clip goes on an instrument track.");
        }
        if (target == at.Track)
        {
            double current = at.IsAudio ? source.Clips[at.AudioIndex].Start : source.MidiClips[at.MidiIndex].Start;
            return Math.Abs(current - start) < TimelineRules.NoMove
                ? CommandResult.Ok(document)
                : ClipEdits.Edit(document, command.Clip, clip => clip with { Start = start }, clip => clip with { Start = start });
        }
        return MoveToTrack(document, at, target, start);
    }

    private static CommandResult MoveToTrack(ProjectDocument document, ClipEdits.Location at, int target, double start)
    {
        Track destination = document.Tracks[target];
        int held = at.IsAudio ? destination.Clips.Count : destination.MidiClips.Count;
        if (held >= ProjectLimits.MaxClips)
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} clips of one kind.");
        }
        ValueList<Track> tracks = [.. document.Tracks.Select((track, position) => position == at.Track ? Without(track, at) : track)];
        ProjectDocument removed = document with { Tracks = tracks };
        Track source = document.Tracks[at.Track];
        return CommandResult.Ok(ClipEdits.WithTrack(removed, target, track => at.IsAudio
            ? track with { Clips = [.. track.Clips, source.Clips[at.AudioIndex] with { Start = start }] }
            : track with { MidiClips = [.. track.MidiClips, source.MidiClips[at.MidiIndex] with { Start = start }] }));
    }

    private static CommandResult Trim(ProjectDocument document, TrimClip command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        if (!at.IsAudio)
        {
            return CommandChecks.Rejected("Only an audio clip is trimmed; set the length of a MIDI clip.");
        }
        if (CommandChecks.Range(nameof(command.Time), command.Time, 0, ProjectLimits.MaxSeconds) is { } refusal)
        {
            return refusal;
        }
        return ClipEdits.EditAudio(
            document,
            command.Clip,
            clip => command.Edge == ClipEdge.Start ? ClipRules.TrimStart(clip, command.Time) : ClipRules.TrimEnd(clip, command.Time));
    }

    private static CommandResult Split(ProjectDocument document, SplitClip command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        if (!at.IsAudio)
        {
            return CommandChecks.Rejected("Only an audio clip is split.");
        }
        if (CommandChecks.Range(nameof(command.At), command.At, 0, ProjectLimits.MaxSeconds) is { } refusal)
        {
            return refusal;
        }
        Track track = document.Tracks[at.Track];
        AudioClip clip = track.Clips[at.AudioIndex];
        if (!ClipRules.CanSplit(clip, command.At))
        {
            return CommandChecks.Rejected("The split point must be inside the clip, away from both ends.");
        }
        if (track.Clips.Count >= ProjectLimits.MaxClips)
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} audio clips.");
        }
        (AudioClip left, AudioClip right) = ClipRules.Split(clip, command.At, ids.Next<ClipId>());
        ValueList<AudioClip> clips = [.. track.Clips.SelectMany((candidate, index) => index == at.AudioIndex ? new[] { left, right } : [candidate])];
        return CommandResult.Ok(ClipEdits.WithTrack(document, at.Track, current => current with { Clips = clips }), right.Id.Value);
    }

    private static CommandResult Duplicate(ProjectDocument document, DuplicateClip command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        if (CommandChecks.Finite((nameof(command.Start), command.Start)) is { } refusal)
        {
            return refusal;
        }
        if (command.Start < 0)
        {
            return CommandChecks.OutOfRange("A copy starts at the beginning of the project or later.");
        }
        Track track = document.Tracks[at.Track];
        if ((at.IsAudio ? track.Clips.Count : track.MidiClips.Count) >= ProjectLimits.MaxClips)
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} clips of one kind.");
        }
        ClipId id = ids.Next<ClipId>();
        if (at.IsAudio)
        {
            AudioClip original = track.Clips[at.AudioIndex];
            AudioClip copy = original with
            {
                Id = id,
                Name = ClipRules.NextCopyName(original.Name),
                Start = command.Start ?? ClipRules.EndOf(original) + ClipRules.CopyGap,
                Meta = ClipRules.LyricsOnly(original.Meta),
            };
            return CommandResult.Ok(ClipEdits.WithTrack(document, at.Track, current => current with { Clips = [.. current.Clips, copy] }), id.Value);
        }
        MidiClip source = track.MidiClips[at.MidiIndex];
        MidiClip midiCopy = source with
        {
            Id = id,
            Name = ClipRules.NextCopyName(source.Name),
            Start = command.Start ?? ClipRules.EndOf(source) + ClipRules.CopyGap,
        };
        return CommandResult.Ok(ClipEdits.WithTrack(document, at.Track, current => current with { MidiClips = [.. current.MidiClips, midiCopy] }), id.Value);
    }

    private static CommandResult Delete(ProjectDocument document, DeleteClip command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        bool shuffle = document.View.EditMode == EditMode.Shuffle;
        ProjectDocument edited = ClipEdits.WithTrack(document, at.Track, track =>
        {
            Track rest = Without(track, at);
            return shuffle ? CloseGap(rest, track, at) : rest;
        });
        if (at.IsAudio)
        {
            edited = edited with { Tracks = [.. ClipLinks.ClearDangling(edited.Tracks)] };
        }
        return CommandResult.Ok(edited);
    }

    private static CommandResult SetGain(ProjectDocument document, SetClipGain command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        return !at.IsAudio
            ? ClipEdits.WrongKind(wantsAudio: true)
            : CommandChecks.Finite((nameof(command.Gain), command.Gain))
                ?? ClipEdits.EditAudio(document, command.Clip, clip => clip with { Gain = command.Gain });
    }

    private static CommandResult SetFades(ProjectDocument document, SetClipFades command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        return !at.IsAudio
            ? ClipEdits.WrongKind(wantsAudio: true)
            : CommandChecks.Range(nameof(command.FadeIn), command.FadeIn, 0, ProjectLimits.MaxSeconds)
                ?? CommandChecks.Range(nameof(command.FadeOut), command.FadeOut, 0, ProjectLimits.MaxSeconds)
                ?? ClipEdits.EditAudio(document, command.Clip, clip => ClipRules.SetFades(clip, command.FadeIn, command.FadeOut));
    }

    private static CommandResult SetMidiLength(ProjectDocument document, SetMidiClipLength command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        if (at.IsAudio)
        {
            return ClipEdits.WrongKind(wantsAudio: false);
        }
        if (CommandChecks.Range(nameof(command.Length), command.Length, MidiClip.MinLength, ProjectLimits.MaxSeconds) is { } refusal)
        {
            return refusal;
        }
        MidiClip held = document.Tracks[at.Track].MidiClips[at.MidiIndex];
        if (held.Notes.Any(note => note.Start >= command.Length) || held.ControlChanges.Any(change => change.Time > command.Length))
        {
            return CommandChecks.Rejected("A note or controller event would fall outside the shorter clip; remove or move it first.");
        }
        return ClipEdits.Edit(document, command.Clip, clip => clip, clip => clip with { Length = command.Length });
    }

    private static CommandResult SetMuted(ProjectDocument document, SetClipMuted command, IIdService ids)
    {
        return ClipEdits.Edit(document, command.Clip, clip => clip with { Muted = command.Muted }, clip => clip with { Muted = command.Muted });
    }

    private static CommandResult Rename(ProjectDocument document, RenameClip command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is null)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        string name = command.Name.Trim();
        return CommandChecks.Named(command.Name)
            ?? ClipEdits.Edit(document, command.Clip, clip => clip with { Name = name }, clip => clip with { Name = name });
    }

    private static CommandResult SetColor(ProjectDocument document, SetClipColor command, IIdService ids)
    {
        return ClipEdits.Edit(document, command.Clip, clip => clip with { Color = command.Color }, clip => clip with { Color = command.Color });
    }

    private static CommandResult Reorder(ProjectDocument document, ReorderClip command, IIdService ids)
    {
        if (ClipEdits.Find(document, command.Clip) is not { } at)
        {
            return ClipEdits.NoClip(command.Clip);
        }
        return CommandResult.Ok(ClipEdits.WithTrack(document, at.Track, track => at.IsAudio
            ? track with { Clips = Layered(track.Clips, at.AudioIndex, command.Layer) }
            : track with { MidiClips = Layered(track.MidiClips, at.MidiIndex, command.Layer) }));
    }

    private static int TrackIndex(ProjectDocument document, TrackId id)
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

    private static Track Without(Track track, ClipEdits.Location at)
    {
        return at.IsAudio
            ? track with { Clips = [.. track.Clips.Where((_, index) => index != at.AudioIndex)] }
            : track with { MidiClips = [.. track.MidiClips.Where((_, index) => index != at.MidiIndex)] };
    }

    // Shuffle closes the gap: the clips of the same kind that began at or after the removed clip's end slide left by its length.
    private static Track CloseGap(Track rest, Track before, ClipEdits.Location at)
    {
        if (at.IsAudio)
        {
            AudioClip removed = before.Clips[at.AudioIndex];
            double length = ClipRules.VisibleLength(removed);
            double end = ClipRules.EndOf(removed);
            return rest with { Clips = [.. rest.Clips.Select(clip => clip.Start >= end - 1e-6 ? clip with { Start = clip.Start - length } : clip)] };
        }
        MidiClip gone = before.MidiClips[at.MidiIndex];
        double midiEnd = ClipRules.EndOf(gone);
        return rest with { MidiClips = [.. rest.MidiClips.Select(clip => clip.Start >= midiEnd - 1e-6 ? clip with { Start = clip.Start - gone.Length } : clip)] };
    }

    private static ValueList<T> Layered<T>(ValueList<T> list, int index, ClipLayer layer)
    {
        T moved = list[index];
        IEnumerable<T> others = list.Where((_, position) => position != index);
        return layer == ClipLayer.Front ? [.. others, moved] : [moved, .. others];
    }
}
