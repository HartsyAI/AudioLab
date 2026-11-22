using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Measures what a project holds.</summary>
public static class ProjectContent
{
    /// <summary>Where the last clip, audio or MIDI, ends in seconds; 0 for a project with none.</summary>
    public static double End(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        double end = 0;
        foreach (Track track in document.Tracks)
        {
            foreach (AudioClip clip in track.Clips)
            {
                end = Math.Max(end, clip.Start + (clip.Duration - clip.Offset - clip.TrimEnd));
            }
            foreach (MidiClip clip in track.MidiClips)
            {
                end = Math.Max(end, clip.Start + clip.Length);
            }
        }
        return end;
    }

    /// <summary>Whether the project holds anything worth keeping: an audio or MIDI clip, or a drum pad.</summary>
    public static bool HasContent(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Tracks.Any(track => track.Clips.Count > 0 || track.MidiClips.Count > 0) || document.BeatPattern.Lanes.Count > 0;
    }

    /// <summary>Every media file the project plays: the clips', the drum pads' and a sampler's sample.</summary>
    /// <remarks>A media store keeps these and may drop the rest.</remarks>
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
