using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Media;

/// <summary>Where a file goes when the keyboard adds it: the selected audio track, at the playhead or after the selected clip.</summary>
public static class ClipInsertion
{
    /// <summary>The placement of <paramref name="file"/>, or the reason there is none.</summary>
    /// <param name="document">The document.</param>
    /// <param name="file">The file to add.</param>
    /// <param name="selectedTrack">The selected track, or null; the first audio track is used when nothing is selected.</param>
    /// <param name="selectedClip">The selected clip, or null; the new clip then starts where that one ends.</param>
    /// <param name="playhead">The playhead time, where the clip starts when no clip is selected.</param>
    public static (MediaPlacement? Placement, string? Refusal) For(
        ProjectDocument document,
        MediaSummary file,
        TrackId? selectedTrack,
        ClipId? selectedClip,
        double playhead)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(file);
        // A selection that names a track that is gone (another client removed it) is no selection.
        Track? track = (selectedTrack is { } chosen ? document.Tracks.FirstOrDefault(candidate => candidate.Id == chosen) : null)
            ?? document.Tracks.FirstOrDefault(candidate => candidate.Kind == TrackKind.Audio);
        if (track is null)
        {
            return (null, "Add an audio track first; a file needs an audio track to go on.");
        }
        if (track.Kind != TrackKind.Audio)
        {
            return (null, $"\"{track.Name}\" is {TrackKindText.Phrase(track.Kind)} and audio goes on an audio track; select an audio track.");
        }
        AudioClip? selected = selectedClip is { } clip ? track.Clips.FirstOrDefault(candidate => candidate.Id == clip) : null;
        double start = selected is null ? Math.Max(0, playhead) : ClipRules.EndOf(selected);
        return (new MediaPlacement(file, track.Id, start), null);
    }
}
