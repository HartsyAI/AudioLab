using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The parts of a project that describe the moment, not the work: which tracks are armed and which are monitored.</summary>
/// <remarks>
/// A track is armed or monitored by whoever is at the controls, so these are neither saved with the project nor taken back by undo.
/// </remarks>
public static class LiveState
{
    /// <summary>The project as it opens: no track armed and none monitored.</summary>
    public static ProjectDocument Reset(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Tracks.Any(track => track.Armed || track.Monitor)
            ? document with { Tracks = [.. document.Tracks.Select(track => track with { Armed = false, Monitor = false })] }
            : document;
    }

    /// <summary>A document taken back from history with the arming and monitoring of <paramref name="current"/>.</summary>
    /// <remarks>A track that history brings back is not armed or monitored.</remarks>
    public static ProjectDocument Carry(ProjectDocument restored, ProjectDocument current)
    {
        ArgumentNullException.ThrowIfNull(restored);
        ArgumentNullException.ThrowIfNull(current);
        Dictionary<TrackId, Track> now = current.Tracks.ToDictionary(track => track.Id);
        ValueList<Track> tracks =
        [
            .. restored.Tracks.Select(track => now.TryGetValue(track.Id, out Track? live)
                ? track with { Armed = live.Armed, Monitor = live.Monitor }
                : track with { Armed = false, Monitor = false }),
        ];
        return restored.Tracks.SequenceEqual(tracks) ? restored : restored with { Tracks = tracks };
    }
}
