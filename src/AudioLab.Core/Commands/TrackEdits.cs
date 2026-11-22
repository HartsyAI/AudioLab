using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Edits of one track that the track and mixer handlers share.</summary>
internal static class TrackEdits
{
    /// <summary>The document with <paramref name="edit"/> applied to the track, or a not-found refusal.</summary>
    public static CommandResult Replace(ProjectDocument document, TrackId id, Func<Track, Track> edit)
    {
        if (!document.Tracks.Any(track => track.Id == id))
        {
            return CommandChecks.NotFound("track", id.Value);
        }
        ValueList<Track> tracks = [.. document.Tracks.Select(track => track.Id == id ? edit(track) : track)];
        return CommandResult.Ok(document with { Tracks = tracks });
    }

    /// <summary>A refusal when the list is empty or names a track the project does not have; null when every track exists.</summary>
    public static CommandResult? CheckTracks(ProjectDocument document, ValueList<TrackId> ids)
    {
        if (ids.Count == 0)
        {
            return CommandChecks.OutOfRange("Name at least one track.");
        }
        HashSet<TrackId> known = [.. document.Tracks.Select(track => track.Id)];
        TrackId missing = ids.FirstOrDefault(id => !known.Contains(id));
        return missing.Value.Length > 0 ? CommandChecks.NotFound("track", missing.Value) : null;
    }
}
