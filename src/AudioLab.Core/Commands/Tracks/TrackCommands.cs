using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The track commands: add, remove, rename, recolour and reorder. A track kind is chosen when it is added.</summary>
internal static class TrackCommands
{
    public static void Register(CommandRegistryBuilder builder, TrackCommandJsonContext json)
    {
        builder
            .Add("track_add", "Adds an empty track at the bottom and allocates its id.", json.AddTrack, Add)
            .Add("track_remove", "Removes a track, the sends into it if it is a bus, and a group it leaves empty.", json.RemoveTrack, Remove)
            .Add("track_rename", "Renames a track.", json.RenameTrack, Rename)
            .Add("track_set_color", "Recolours a track.", json.SetTrackColor, SetColor)
            .Add("track_move", "Moves a track one step in the track list.", json.MoveTrack, Move);
    }

    private static CommandResult Add(ProjectDocument document, AddTrack command, IIdService ids)
    {
        TrackKind kind = command.Kind ?? TrackKind.Audio;
        if (CommandChecks.TooLong(command.Name) is { } tooLong)
        {
            return tooLong;
        }
        if (command.Preset is not null && kind != TrackKind.Instrument)
        {
            return CommandChecks.Rejected("A preset belongs to an instrument track.");
        }
        string preset = command.Preset ?? InstrumentPresets.DefaultSynth;
        if (kind == TrackKind.Instrument && !InstrumentPresets.Names.Contains(preset))
        {
            return CommandChecks.OutOfRange($"The preset must be one of: {string.Join(", ", InstrumentPresets.Names)}.");
        }
        Instrument? instrument = kind != TrackKind.Instrument
            ? null
            : preset == InstrumentPresets.Sampler ? InstrumentPresets.CreateSampler() : InstrumentPresets.CreateSynth(preset);
        string prefix = kind switch
        {
            TrackKind.Bus => "Bus",
            TrackKind.Instrument => preset,
            _ => "Track",
        };
        string name = string.IsNullOrWhiteSpace(command.Name) ? GroupRules.NextTrackName(document.Tracks, prefix) : command.Name.Trim();
        string color = command.Color ?? ProjectDefaults.TrackColor(document.Tracks.Count);
        Track added = ProjectDefaults.NewTrack(ids.Next<TrackId>(), name, kind, color, instrument);
        return CommandResult.Ok(document with { Tracks = [.. document.Tracks, added] }, added.Id.Value);
    }

    private static CommandResult Remove(ProjectDocument document, RemoveTrack command, IIdService ids)
    {
        Track? removed = document.Tracks.FirstOrDefault(track => track.Id == command.Track);
        if (removed is null)
        {
            return CommandChecks.NotFound("track", command.Track.Value);
        }
        IEnumerable<Track> remaining = document.Tracks
            .Where(track => track.Id != command.Track)
            .Select(track => removed.Kind == TrackKind.Bus ? track with { Sends = [.. track.Sends.Where(send => send.Bus != removed.Id)] } : track);
        GroupedTracks grouped = GroupRules.Normalize(remaining, document.Groups);
        return CommandResult.Ok(document with { Tracks = [.. grouped.Tracks], Groups = [.. grouped.Groups] });
    }

    private static CommandResult Rename(ProjectDocument document, RenameTrack command, IIdService ids)
    {
        return CommandChecks.Named(command.Name)
            ?? TrackEdits.Replace(document, command.Track, track => track with { Name = command.Name.Trim() });
    }

    private static CommandResult SetColor(ProjectDocument document, SetTrackColor command, IIdService ids)
    {
        return TrackEdits.Replace(document, command.Track, track => track with { Color = command.Color });
    }

    private static CommandResult Move(ProjectDocument document, MoveTrack command, IIdService ids)
    {
        if (!document.Tracks.Any(track => track.Id == command.Track))
        {
            return CommandChecks.NotFound("track", command.Track.Value);
        }
        IReadOnlyList<Track>? moved = GroupRules.MoveTrack(document.Tracks, command.Track, command.Direction);
        return CommandResult.Ok(moved is null ? document : document with { Tracks = [.. moved] });
    }
}
