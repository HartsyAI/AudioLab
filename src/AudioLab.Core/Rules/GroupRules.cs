using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The group rules: a group's members sit together in the track list, an empty group does not exist, groups do not nest.</summary>
public static class GroupRules
{
    /// <summary>The group a track belongs to, or null.</summary>
    public static TrackGroup? GroupOf(Track track, IEnumerable<TrackGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(groups);
        return track.Group is { } id ? groups.FirstOrDefault(group => group.Id == id) : null;
    }

    /// <summary>The members of a group, in track order.</summary>
    public static IReadOnlyList<Track> MembersOf(GroupId group, IEnumerable<Track> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        return [.. tracks.Where(track => track.Group == group)];
    }

    /// <summary>A name like "Group 2" that no group uses yet.</summary>
    public static string UniqueName(IReadOnlyList<TrackGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        HashSet<string> used = [.. groups.Select(group => group.Name)];
        for (int number = groups.Count + 1; ; number++)
        {
            string name = $"Group {number.ToString(CultureInfo.InvariantCulture)}";
            if (!used.Contains(name))
            {
                return name;
            }
        }
    }

    /// <summary>Puts the list in order: every group's members sit together where its first member was, a track pointing at a group that does not exist leaves it, and a group with no members is dropped.</summary>
    public static GroupedTracks Normalize(IEnumerable<Track> tracks, IEnumerable<TrackGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(groups);
        List<TrackGroup> known = [.. groups];
        HashSet<GroupId> ids = [.. known.Select(group => group.Id)];
        List<Track> live = [.. tracks.Select(track => track.Group is { } id && !ids.Contains(id) ? track with { Group = null } : track)];
        HashSet<GroupId> placed = [];
        List<Track> ordered = [];
        foreach (Track track in live)
        {
            if (track.Group is not { } group)
            {
                ordered.Add(track);
            }
            else if (placed.Add(group))
            {
                ordered.AddRange(live.Where(member => member.Group == group));
            }
        }
        return new GroupedTracks(ordered, [.. known.Where(group => placed.Contains(group.Id))]);
    }

    /// <summary>Puts the tracks with these ids into <paramref name="group"/>, or out of their group when it is null, then normalizes.</summary>
    public static GroupedTracks Assign(IEnumerable<Track> tracks, IEnumerable<TrackGroup> groups, IReadOnlyCollection<TrackId> ids, GroupId? group)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(ids);
        HashSet<TrackId> chosen = [.. ids];
        return Normalize(tracks.Select(track => chosen.Contains(track.Id) ? track with { Group = group } : track), groups);
    }

    /// <summary>The track list as blocks, top to bottom; moving a block never splits a group.</summary>
    public static IReadOnlyList<TrackBlock> Blocks(IEnumerable<Track> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        List<(GroupId? Group, List<Track> Tracks)> blocks = [];
        Dictionary<GroupId, List<Track>> byGroup = [];
        foreach (Track track in tracks)
        {
            if (track.Group is not { } group)
            {
                blocks.Add((null, [track]));
            }
            else if (byGroup.TryGetValue(group, out List<Track>? members))
            {
                members.Add(track);
            }
            else
            {
                List<Track> started = [track];
                byGroup[group] = started;
                blocks.Add((group, started));
            }
        }
        return [.. blocks.Select(block => new TrackBlock(block.Group, block.Tracks))];
    }

    /// <summary>Moves a track one step.</summary>
    /// <remarks>Inside a group it swaps with its neighbour in the group and stops at the ends; outside, it steps over the next block (a whole group counts as one). Null when it cannot move.</remarks>
    public static IReadOnlyList<Track>? MoveTrack(IReadOnlyList<Track> tracks, TrackId id, MoveDirection direction)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        Track? track = tracks.FirstOrDefault(candidate => candidate.Id == id);
        if (track is null)
        {
            return null;
        }
        List<List<Track>> blocks = [.. Blocks(tracks).Select(block => block.Tracks.ToList())];
        int blockIndex = blocks.FindIndex(block => block.Contains(track));
        int step = direction == MoveDirection.Down ? 1 : -1;
        if (track.Group is not null)
        {
            List<Track> members = blocks[blockIndex];
            int from = members.IndexOf(track);
            int to = from + step;
            if (to < 0 || to >= members.Count)
            {
                return null;
            }
            (members[from], members[to]) = (members[to], members[from]);
        }
        else
        {
            int to = blockIndex + step;
            if (to < 0 || to >= blocks.Count)
            {
                return null;
            }
            (blocks[blockIndex], blocks[to]) = (blocks[to], blocks[blockIndex]);
        }
        return [.. blocks.SelectMany(block => block)];
    }

    /// <summary>Moves a whole group past the neighbouring block. Null at the ends or when there is no such group.</summary>
    public static IReadOnlyList<Track>? MoveGroup(IReadOnlyList<Track> tracks, GroupId group, MoveDirection direction)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        List<List<Track>> blocks = [];
        int blockIndex = -1;
        foreach (TrackBlock block in Blocks(tracks))
        {
            if (block.Group == group)
            {
                blockIndex = blocks.Count;
            }
            blocks.Add([.. block.Tracks]);
        }
        int to = blockIndex + (direction == MoveDirection.Down ? 1 : -1);
        if (blockIndex < 0 || to < 0 || to >= blocks.Count)
        {
            return null;
        }
        (blocks[blockIndex], blocks[to]) = (blocks[to], blocks[blockIndex]);
        return [.. blocks.SelectMany(block => block)];
    }

    /// <summary>The lowest-numbered name like "Track 2" that no track uses yet.</summary>
    public static string NextTrackName(IEnumerable<Track> tracks, string prefix)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(prefix);
        HashSet<string> used = [.. tracks.Select(track => track.Name)];
        for (int number = 1; ; number++)
        {
            string name = $"{prefix} {number.ToString(CultureInfo.InvariantCulture)}";
            if (!used.Contains(name))
            {
                return name;
            }
        }
    }
}
