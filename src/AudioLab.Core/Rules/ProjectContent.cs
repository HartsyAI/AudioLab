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
}
