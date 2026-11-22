using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The track settings commands: record arm, input monitoring, the input, the lane height and the icon.</summary>
internal static class TrackSettingsCommands
{
    public static void Register(CommandRegistryBuilder builder, TrackCommandJsonContext json)
    {
        builder
            .Add("track_set_armed", "Arms a track to record, or disarms it.", json.SetTrackArmed, SetArmed)
            .Add("track_set_monitor", "Switches input monitoring through a track on or off.", json.SetTrackMonitor, SetMonitor)
            .Add("track_set_input", "Sets the device and the channels a track records from.", json.SetTrackInput, SetInput)
            .Add("track_set_height", "Sets the height of a track's lane.", json.SetTrackHeight, SetHeight)
            .Add("track_set_icon", "Sets the icon of a track, or removes it.", json.SetTrackIcon, SetIcon);
    }

    private static CommandResult SetArmed(ProjectDocument document, SetTrackArmed command, IIdService ids)
    {
        return Input(document, command.Track, command.Armed)
            ?? TrackEdits.Replace(document, command.Track, track => track with { Armed = command.Armed });
    }

    private static CommandResult SetMonitor(ProjectDocument document, SetTrackMonitor command, IIdService ids)
    {
        return Input(document, command.Track, command.Monitor)
            ?? TrackEdits.Replace(document, command.Track, track => track with { Monitor = command.Monitor });
    }

    private static CommandResult SetInput(ProjectDocument document, SetTrackInput command, IIdService ids)
    {
        if (Input(document, command.Track, true) is { } refused)
        {
            return refused;
        }
        bool single = command.Channels == InputChannelMode.Single;
        if (single && command.Channel is null)
        {
            return CommandChecks.OutOfRange("A single channel needs its number, 1 to 256.");
        }
        if (!single && command.Channel is not null)
        {
            return CommandChecks.Rejected("Only a single channel has a number; leave the channel out for mono and stereo.");
        }
        TrackInput input = new(command.DeviceId, command.Channels, command.Channel);
        return CommandChecks.Length(nameof(command.DeviceId), command.DeviceId, 512)
            ?? CommandChecks.Range(nameof(command.Channel), command.Channel, 1, 256)
            ?? TrackEdits.Replace(document, command.Track, track => track with { Input = input });
    }

    private static CommandResult SetHeight(ProjectDocument document, SetTrackHeight command, IIdService ids)
    {
        return TrackEdits.Exists(document, command.Track)
            ?? CommandChecks.Range(nameof(command.Height), command.Height, TrackRules.MinHeight, TrackRules.MaxHeight)
            ?? TrackEdits.Replace(document, command.Track, track => track with { View = track.View with { Height = command.Height } });
    }

    private static CommandResult SetIcon(ProjectDocument document, SetTrackIcon command, IIdService ids)
    {
        if (TrackEdits.Exists(document, command.Track) is { } missing)
        {
            return missing;
        }
        if (command.Icon is not null && !TrackIcon.IsValid(command.Icon))
        {
            return CommandChecks.OutOfRange("An icon is short text, up to 16 characters, or a small png, jpeg or webp image as a data URL.");
        }
        return TrackEdits.Replace(document, command.Track, track => track with { Icon = command.Icon });
    }

    // A refusal when the track is missing, or is an aux bus and the command turns an input on: a bus has none to record or monitor.
    private static CommandResult? Input(ProjectDocument document, TrackId id, bool turnsOn)
    {
        if (TrackEdits.Exists(document, id) is { } missing)
        {
            return missing;
        }
        Track track = document.Tracks.First(candidate => candidate.Id == id);
        return turnsOn && !TrackRules.HasInput(track.Kind) ? CommandChecks.Rejected("An aux bus has no input to record or monitor.") : null;
    }
}
