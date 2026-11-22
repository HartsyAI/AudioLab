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

    /// <summary>Whether making <paramref name="parent"/> the score parent of <paramref name="clip"/> would close a loop of parents.</summary>
    public static bool ParentLoops(IEnumerable<Track> tracks, ClipId clip, ClipId parent)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        Dictionary<ClipId, ClipId> parents = [];
        foreach (AudioClip candidate in tracks.SelectMany(track => track.Clips))
        {
            if (candidate.Meta?.Score?.Parent is { } link)
            {
                parents[candidate.Id] = link;
            }
        }
        HashSet<ClipId> seen = [];
        ClipId current = parent;
        while (current != clip)
        {
            if (!seen.Add(current) || !parents.TryGetValue(current, out ClipId next))
            {
                return false;
            }
            current = next;
        }
        return true;
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
