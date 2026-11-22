using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Links between clips: a score version points at its parent and a stem at the clip it came from.</summary>
public static class ClipLinks
{
    /// <summary>The tracks with every link to a clip that no longer exists cleared; a track nothing changes is returned as it was.</summary>
    public static IReadOnlyList<Track> ClearDangling(IEnumerable<Track> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        List<Track> all = [.. tracks];
        HashSet<ClipId> existing = [.. all.SelectMany(track => track.Clips).Select(clip => clip.Id)];
        return [.. all.Select(track => ClearIn(track, existing))];
    }

    private static Track ClearIn(Track track, HashSet<ClipId> existing)
    {
        if (!track.Clips.Any(clip => Dangles(clip.Meta, existing)))
        {
            return track;
        }
        ValueList<AudioClip> clips = [.. track.Clips.Select(clip => Dangles(clip.Meta, existing) ? clip with { Meta = Cleared(clip.Meta!, existing) } : clip)];
        return track with { Clips = clips };
    }

    private static bool Dangles(ClipMeta? meta, HashSet<ClipId> existing)
    {
        return meta is not null && (Missing(meta.Score?.Parent, existing) || Missing(meta.StemOf?.ClipId, existing));
    }

    private static bool Missing(ClipId? link, HashSet<ClipId> existing) => link is { } id && !existing.Contains(id);

    private static ClipMeta Cleared(ClipMeta meta, HashSet<ClipId> existing)
    {
        ScoreMeta? score = meta.Score;
        if (Missing(score?.Parent, existing))
        {
            score = score! with { Parent = null };
        }
        StemOf? stem = meta.StemOf;
        if (Missing(stem?.ClipId, existing))
        {
            stem = stem!.Value with { ClipId = null };
        }
        return meta with { Score = score, StemOf = stem };
    }
}
