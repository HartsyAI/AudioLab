using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Measures what a project holds.</summary>
public static class ProjectContent
{
    /// <summary>Where the last clip, audio or MIDI, ends in seconds; 0 for a project with none.</summary>
    /// <remarks>
    /// With <paramref name="audibleOnly"/> a muted clip does not count, nor does a clip of a track that is muted or silent under solo
    /// and sends nothing that is heard, so a render ends where the sound does; <paramref name="ignoreSolo"/> leaves solo out of that,
    /// as a stem does.
    /// </remarks>
    public static double End(ProjectDocument document, bool audibleOnly = false, bool ignoreSolo = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        double end = 0;
        foreach (Track track in document.Tracks)
        {
            if (audibleOnly && !IsHeard(document, track, ignoreSolo))
            {
                continue;
            }
            foreach (AudioClip clip in track.Clips.Where(clip => !audibleOnly || !clip.Muted))
            {
                end = Math.Max(end, clip.Start + (clip.Duration - clip.Offset - clip.TrimEnd));
            }
            foreach (MidiClip clip in track.MidiClips.Where(clip => !audibleOnly || !clip.Muted))
            {
                end = Math.Max(end, clip.Start + clip.Length);
            }
        }
        return end;
    }

    // A track is heard when it is not muted and not silenced by solo, or when a send of it is heard.
    private static bool IsHeard(ProjectDocument document, Track track, bool ignoreSolo) =>
        (!MixRules.IsMuted(track, document.Groups) && (ignoreSolo || MixRules.IsSoloAudible(document, track)))
        || track.Sends.Any(send => MixRules.SendGain(document, track, send, ignoreSolo) > 0);

    /// <summary>Whether the project holds anything worth keeping: an audio or MIDI clip, or a drum pad.</summary>
    public static bool HasContent(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Tracks.Any(track => track.Clips.Count > 0 || track.MidiClips.Count > 0) || document.BeatPattern.Lanes.Count > 0;
    }

    /// <summary>Every media file the project plays: the clips', the drum pads' and a sampler's sample.</summary>
    /// <remarks>
    /// A media store keeps these, and must apply the rule to every history document too: an undo step can play a file the project no longer does.
    /// </remarks>
    public static IReadOnlySet<MediaId> ReferencedMedia(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        HashSet<MediaId> media = [.. document.Tracks.SelectMany(track => track.Clips).Select(clip => clip.Media)];
        media.UnionWith(document.BeatPattern.Lanes.Where(lane => lane.Media is not null).Select(lane => lane.Media!.Value));
        media.UnionWith(document.Tracks
            .Select(track => track.Instrument)
            .OfType<SamplerInstrument>()
            .Where(sampler => sampler.Sample is not null)
            .Select(sampler => sampler.Sample!.Value));
        return media;
    }
}
