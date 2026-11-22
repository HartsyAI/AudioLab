using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The mixer commands: the fader, pan, mute and solo of a track, the master, and the sends to aux buses.</summary>
internal static class MixerCommands
{
    // A new send starts at -6 dB, post-fader.
    private const double DefaultSendLevel = 0.5;

    public static void Register(CommandRegistryBuilder builder, TrackCommandJsonContext json)
    {
        builder
            .Add("mixer_set_track_volume", "Sets the fader of a track.", json.SetTrackVolume, SetVolume)
            .Add("mixer_set_track_pan", "Sets the pan of a track.", json.SetTrackPan, SetPan)
            .Add("mixer_set_track_muted", "Mutes or unmutes a track.", json.SetTrackMuted, SetMuted)
            .Add("mixer_set_track_soloed", "Solos or unsolos a track.", json.SetTrackSoloed, SetSoloed)
            .Add("mixer_set_master_volume", "Sets the master fader.", json.SetMasterVolume, SetMasterVolume)
            .Add("mixer_set_master_limiter", "Switches the master limiter on or off.", json.SetMasterLimiter, SetMasterLimiter)
            .Add("send_add", "Routes a track to an aux bus, post-fader.", json.AddSend, AddSend)
            .Add("send_remove", "Removes the send of a track to a bus.", json.RemoveSend, RemoveSend)
            .Add("send_set_level", "Sets the level of a send.", json.SetSendLevel, SetSendLevel)
            .Add("send_set_pre", "Takes a send before or after the fader.", json.SetSendPre, SetSendPre)
            .Add("send_set_muted", "Mutes or unmutes a send.", json.SetSendMuted, SetSendMuted);
    }

    private static CommandResult SetVolume(ProjectDocument document, SetTrackVolume command, IIdService ids)
    {
        return CommandChecks.Finite((nameof(command.Volume), command.Volume))
            ?? TrackEdits.Replace(document, command.Track, track => track with { Volume = command.Volume });
    }

    private static CommandResult SetPan(ProjectDocument document, SetTrackPan command, IIdService ids)
    {
        return CommandChecks.Finite((nameof(command.Pan), command.Pan))
            ?? TrackEdits.Replace(document, command.Track, track => track with { Pan = command.Pan });
    }

    private static CommandResult SetMuted(ProjectDocument document, SetTrackMuted command, IIdService ids)
    {
        return TrackEdits.Replace(document, command.Track, track => track with { Muted = command.Muted });
    }

    private static CommandResult SetSoloed(ProjectDocument document, SetTrackSoloed command, IIdService ids)
    {
        return TrackEdits.Replace(document, command.Track, track => track with { Soloed = command.Soloed });
    }

    private static CommandResult SetMasterVolume(ProjectDocument document, SetMasterVolume command, IIdService ids)
    {
        return CommandChecks.Finite((nameof(command.Volume), command.Volume))
            ?? CommandResult.Ok(document with { MasterVolume = command.Volume });
    }

    private static CommandResult SetMasterLimiter(ProjectDocument document, SetMasterLimiter command, IIdService ids)
    {
        return CommandResult.Ok(document with { MasterLimiter = command.Enabled });
    }

    private static CommandResult AddSend(ProjectDocument document, AddSend command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Level), command.Level)) is { } refusal)
        {
            return refusal;
        }
        Track? track = document.Tracks.FirstOrDefault(candidate => candidate.Id == command.Track);
        if (track is null)
        {
            return CommandChecks.NotFound("track", command.Track.Value);
        }
        Track? bus = document.Tracks.FirstOrDefault(candidate => candidate.Id == command.Bus);
        if (bus is null)
        {
            return CommandChecks.NotFound("track", command.Bus.Value);
        }
        if (track.Kind == TrackKind.Bus)
        {
            return CommandChecks.Rejected("A bus has no sends.");
        }
        if (bus.Kind != TrackKind.Bus)
        {
            return CommandChecks.Rejected("A send goes to a bus track.");
        }
        if (track.Sends.Any(send => send.Bus == command.Bus))
        {
            return CommandChecks.Rejected("The track already sends to this bus.");
        }
        Send added = new(command.Bus, command.Level ?? DefaultSendLevel, false, false);
        return TrackEdits.Replace(document, command.Track, candidate => candidate with { Sends = [.. candidate.Sends, added] });
    }

    private static CommandResult RemoveSend(ProjectDocument document, RemoveSend command, IIdService ids)
    {
        return EditSend(document, command.Track, command.Bus, remove: true, send => send);
    }

    private static CommandResult SetSendLevel(ProjectDocument document, SetSendLevel command, IIdService ids)
    {
        return CommandChecks.Finite((nameof(command.Level), command.Level))
            ?? EditSend(document, command.Track, command.Bus, remove: false, send => send with { Level = command.Level });
    }

    private static CommandResult SetSendPre(ProjectDocument document, SetSendPre command, IIdService ids)
    {
        return EditSend(document, command.Track, command.Bus, remove: false, send => send with { Pre = command.Pre });
    }

    private static CommandResult SetSendMuted(ProjectDocument document, SetSendMuted command, IIdService ids)
    {
        return EditSend(document, command.Track, command.Bus, remove: false, send => send with { Muted = command.Muted });
    }

    // The send is found by its track and bus; the edit replaces it, or it is removed.
    private static CommandResult EditSend(ProjectDocument document, TrackId trackId, TrackId bus, bool remove, Func<Send, Send> edit)
    {
        Track? track = document.Tracks.FirstOrDefault(candidate => candidate.Id == trackId);
        if (track is null)
        {
            return CommandChecks.NotFound("track", trackId.Value);
        }
        if (!track.Sends.Any(send => send.Bus == bus))
        {
            return CommandChecks.NotFound("send to the bus", bus.Value);
        }
        IEnumerable<Send> sends = remove
            ? track.Sends.Where(send => send.Bus != bus)
            : track.Sends.Select(send => send.Bus == bus ? edit(send) : send);
        return TrackEdits.Replace(document, trackId, candidate => candidate with { Sends = [.. sends] });
    }
}
