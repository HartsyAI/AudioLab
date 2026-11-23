using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The range commands: a time range of some tracks removed or cut at its edges, as the old DAW's selection did.</summary>
internal static class RangeCommands
{
    private static readonly ClipId _counting = ClipId.From("clp_counting");

    public static void Register(CommandRegistryBuilder builder, ClipCommandJsonContext json)
    {
        builder
            .Add("range_delete", "Removes a time range from the clips of some tracks; shuffle edit mode closes the gap.", json.DeleteRange, Delete)
            .Add("range_split", "Splits every clip of some tracks at both edges of a time range.", json.SplitAtRange, Split);
    }

    private static CommandResult Delete(ProjectDocument document, DeleteRange command, IIdService ids)
    {
        if (Check(document, command.Tracks, command.Start, command.End, out List<Track> tracks) is { } refused)
        {
            return refused;
        }
        bool ripple = document.View.EditMode == EditMode.Shuffle;
        foreach (Track track in tracks)
        {
            if (RangeRules.Delete(track.Clips, command.Start, command.End, ripple, () => _counting).Count > ProjectLimits.MaxClips)
            {
                return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} audio clips.");
            }
            if (MidiLimit(track, command.Start, command.End, ripple) is { } full)
            {
                return full;
            }
        }
        List<string> created = [];
        ClipId Next()
        {
            ClipId id = ids.Next<ClipId>();
            created.Add(id.Value);
            return id;
        }
        HashSet<TrackId> chosen = [.. tracks.Select(track => track.Id)];
        ValueList<Track> edited =
        [
            .. document.Tracks.Select(track => !chosen.Contains(track.Id)
                ? track
                : track with
                {
                    Clips = RangeRules.Delete(track.Clips, command.Start, command.End, ripple, Next),
                    MidiClips = RangeRules.Delete(track.MidiClips, command.Start, command.End, ripple),
                }),
        ];
        ProjectDocument result = document with { Tracks = [.. ClipLinks.ClearDangling(edited)] };
        return CommandResult.Ok(result, [.. created]);
    }

    // A refusal when a MIDI clip of the track would pass its note limit or use up its note ids, else null.
    private static CommandResult? MidiLimit(Track track, double start, double end, bool ripple)
    {
        foreach (MidiClip clip in track.MidiClips)
        {
            if (clip.NextNoteId.Value + MidiRangeRules.NewNotes(clip, start, end) > NoteId.Max
                || (MidiRangeRules.Delete(clip, start, end, ripple)?.Notes.Count ?? 0) > ProjectLimits.MaxListItems)
            {
                return CommandChecks.OutOfRange($"The MIDI clip {clip.Id.Value} would hold more notes than a clip may, or use every note id.");
            }
        }
        return null;
    }

    private static CommandResult Split(ProjectDocument document, SplitAtRange command, IIdService ids)
    {
        if (Check(document, command.Tracks, command.Start, command.End, out List<Track> tracks) is { } refused)
        {
            return refused;
        }
        if (tracks.Any(track => RangeRules.Split(track.Clips, command.Start, command.End, () => _counting).Count > ProjectLimits.MaxClips))
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxClips} audio clips.");
        }
        List<string> created = [];
        ClipId Next()
        {
            ClipId id = ids.Next<ClipId>();
            created.Add(id.Value);
            return id;
        }
        HashSet<TrackId> chosen = [.. tracks.Select(track => track.Id)];
        ValueList<Track> edited =
        [
            .. document.Tracks.Select(track => chosen.Contains(track.Id)
                ? track with { Clips = RangeRules.Split(track.Clips, command.Start, command.End, Next) }
                : track),
        ];
        return CommandResult.Ok(document with { Tracks = edited }, [.. created]);
    }

    // The tracks of the range, each once and in the project's order, or the refusal: the tracks are looked up first, then the times.
    private static CommandResult? Check(ProjectDocument document, ValueList<TrackId> ids, double start, double end, out List<Track> tracks)
    {
        tracks = [];
        if (TrackEdits.CheckTracks(document, ids) is { } missing)
        {
            return missing;
        }
        HashSet<TrackId> wanted = [.. ids];
        tracks = [.. document.Tracks.Where(track => wanted.Contains(track.Id))];
        CommandResult? invalid = CommandChecks.Range(nameof(start), start, 0, ProjectLimits.MaxSeconds)
            ?? CommandChecks.Range(nameof(end), end, 0, ProjectLimits.MaxSeconds);
        if (invalid is not null)
        {
            return invalid;
        }
        return !ClipRules.FitsPiece(end - start)
            ? CommandChecks.OutOfRange($"The range must be at least {RangeRules.MinLength} seconds long.")
            : null;
    }
}
