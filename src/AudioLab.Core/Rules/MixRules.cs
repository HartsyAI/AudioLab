using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>What a track's mute, solo and gain mean once its group and the aux buses are counted. The engine and the UI read these.</summary>
public static class MixRules
{
    /// <summary>The level a new send starts at: -6 dB, after the track's fader.</summary>
    public const double DefaultSendLevel = 0.5;

    /// <summary>Whether a track is silent by its own mute or its group's.</summary>
    public static bool IsMuted(Track track, IEnumerable<TrackGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.Muted || GroupRules.GroupOf(track, groups)?.Muted == true;
    }

    /// <summary>Whether a track counts as soloed: soloed itself, or in a soloed group.</summary>
    public static bool IsSoloed(Track track, IEnumerable<TrackGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.Soloed || GroupRules.GroupOf(track, groups)?.Soloed == true;
    }

    /// <summary>The track's gain: its fader times its group's fader. A mute is applied by the caller.</summary>
    public static double TrackLevel(Track track, IEnumerable<TrackGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(track);
        TrackGroup? group = GroupRules.GroupOf(track, groups);
        return track.Volume * (group is null || !double.IsFinite(group.Volume) ? 1 : Math.Max(0, group.Volume));
    }

    /// <summary>Whether any track is soloed.</summary>
    public static bool HasSolo(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Tracks.Any(track => IsSoloed(track, document.Groups));
    }

    /// <summary>A bus stays audible under solo while a soloed track sends to it through an unmuted send.</summary>
    public static bool IsBusFedBySolo(ProjectDocument document, Track bus)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bus);
        return document.Tracks.Any(track =>
            IsSoloed(track, document.Groups)
            && track.Kind != TrackKind.Bus
            && track.Sends.Any(send => send.Bus == bus.Id && !send.Muted));
    }

    /// <summary>Why a track is silent, as the mix counts it: its own mute, then its group's, then a solo it is not heard through.</summary>
    /// <remarks><see cref="MixSilence.None"/> when it is heard.</remarks>
    public static MixSilence SilenceOf(ProjectDocument document, Track track)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(track);
        if (track.Muted)
        {
            return MixSilence.TrackMuted;
        }
        if (GroupRules.GroupOf(track, document.Groups)?.Muted == true)
        {
            return MixSilence.GroupMuted;
        }
        // Heard as ProjectContent counts it: its direct output under solo, or an unmuted send into a soloed bus.
        bool heard = IsSoloAudible(document, track) || track.Sends.Any(send => SendGain(document, track, send) > 0);
        return heard ? MixSilence.None : MixSilence.SoloElsewhere;
    }

    /// <summary>Whether a track is heard only through a send into a soloed bus, while a solo elsewhere would otherwise silence it.</summary>
    public static bool IsHeardThroughBus(ProjectDocument document, Track track) =>
        SilenceOf(document, track) == MixSilence.None && !IsSoloAudible(document, track);

    /// <summary>Whether a track's direct output is heard under solo: nothing is soloed, it is soloed, or it is a bus a soloed track feeds.</summary>
    public static bool IsSoloAudible(ProjectDocument document, Track track)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(track);
        return !HasSolo(document)
            || IsSoloed(track, document.Groups)
            || (track.Kind == TrackKind.Bus && IsBusFedBySolo(document, track));
    }

    /// <summary>One send's gain: its level, silent when the send or its source is muted.</summary>
    /// <remarks>A pre-fader send skips the fader, never the mute. Under solo a send still speaks when its bus is soloed, so soloing a return hears what feeds it.</remarks>
    public static double SendGain(ProjectDocument document, Track track, Send send, bool ignoreSolo = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(track);
        if (send.Muted || IsMuted(track, document.Groups))
        {
            return 0;
        }
        Track? bus = document.Tracks.FirstOrDefault(candidate => candidate.Id == send.Bus);
        bool busSoloed = bus is not null && IsSoloed(bus, document.Groups);
        if (!ignoreSolo && HasSolo(document) && !IsSoloed(track, document.Groups) && !busSoloed)
        {
            return 0;
        }
        return send.Level;
    }
}
