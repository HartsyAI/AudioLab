using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class TrackRuleTests
{
    private static readonly GroupId _g1 = GroupId.From("grp_1");

    private static readonly GroupId _g2 = GroupId.From("grp_2");

    private static Track T(string id, GroupId? group = null, string? name = null)
    {
        return BlankProject.Track(id, name ?? id) with { Group = group };
    }

    private static TrackGroup G(GroupId id, string name = "G")
    {
        return new TrackGroup { Id = id, Name = name, Color = "#7a8cff", Collapsed = false, Tint = true, Muted = false, Soloed = false, Volume = 1 };
    }

    private static string[] Ids(IEnumerable<Track> tracks) => [.. tracks.Select(track => track.Id.Value)];

    [Fact]
    public void MembersAreGatheredWhereTheFirstMemberWas()
    {
        Track[] tracks = [T("a"), T("b", _g1), T("c"), T("d", _g1), T("e")];

        GroupedTracks result = GroupRules.Normalize(tracks, [G(_g1)]);

        Assert.Equal(["a", "b", "d", "c", "e"], Ids(result.Tracks));
        Assert.Single(result.Groups);
    }

    [Fact]
    public void AnEmptyGroupIsDroppedAndATrackOfAMissingGroupLeavesIt()
    {
        Track[] tracks = [T("a", _g2), T("b")];

        GroupedTracks result = GroupRules.Normalize(tracks, [G(_g1)]);

        Assert.Empty(result.Groups);
        Assert.All(result.Tracks, track => Assert.Null(track.Group));
    }

    [Fact]
    public void AssigningMovesTracksIntoAGroupAndOutOfIt()
    {
        Track[] tracks = [T("a"), T("b"), T("c")];

        GroupedTracks into = GroupRules.Assign(tracks, [G(_g1)], [TrackId.From("a"), TrackId.From("c")], _g1);
        GroupedTracks outOf = GroupRules.Assign(into.Tracks, into.Groups, [TrackId.From("a"), TrackId.From("c")], null);

        Assert.Equal(["a", "c", "b"], Ids(into.Tracks));
        Assert.Empty(outOf.Groups);
    }

    [Fact]
    public void BlocksKeepAGroupWhole()
    {
        IReadOnlyList<TrackBlock> blocks = GroupRules.Blocks([T("a"), T("b", _g1), T("c", _g1), T("d")]);

        Assert.Equal([null, _g1, null], blocks.Select(block => block.Group));
        Assert.Equal([1, 2, 1], blocks.Select(block => block.Tracks.Count));
    }

    [Fact]
    public void ATrackInsideAGroupSwapsWithItsNeighbourAndStopsAtTheEnds()
    {
        Track[] tracks = [T("a"), T("b", _g1), T("c", _g1), T("d")];

        Assert.Equal(["a", "c", "b", "d"], Ids(GroupRules.MoveTrack(tracks, TrackId.From("b"), MoveDirection.Down)!));
        Assert.Null(GroupRules.MoveTrack(tracks, TrackId.From("b"), MoveDirection.Up));
        Assert.Null(GroupRules.MoveTrack(tracks, TrackId.From("c"), MoveDirection.Down));
    }

    [Fact]
    public void ATrackOutsideAGroupStepsOverAWholeGroup()
    {
        Track[] tracks = [T("a"), T("b", _g1), T("c", _g1), T("d")];

        Assert.Equal(["b", "c", "a", "d"], Ids(GroupRules.MoveTrack(tracks, TrackId.From("a"), MoveDirection.Down)!));
        Assert.Equal(["a", "d", "b", "c"], Ids(GroupRules.MoveTrack(tracks, TrackId.From("d"), MoveDirection.Up)!));
        Assert.Null(GroupRules.MoveTrack(tracks, TrackId.From("a"), MoveDirection.Up));
        Assert.Null(GroupRules.MoveTrack(tracks, TrackId.From("none"), MoveDirection.Up));
    }

    [Fact]
    public void AWholeGroupMovesPastTheNeighbouringBlock()
    {
        Track[] tracks = [T("a"), T("b", _g1), T("c", _g1), T("d")];

        Assert.Equal(["b", "c", "a", "d"], Ids(GroupRules.MoveGroup(tracks, _g1, MoveDirection.Up)!));
        Assert.Equal(["a", "d", "b", "c"], Ids(GroupRules.MoveGroup(tracks, _g1, MoveDirection.Down)!));
        Assert.Null(GroupRules.MoveGroup(tracks, _g2, MoveDirection.Down));
        Assert.Null(GroupRules.MoveGroup([T("x", _g1)], _g1, MoveDirection.Up));
    }

    [Fact]
    public void ANameIsTheLowestFreeNumberAndAGroupNameCountsFromTheGroups()
    {
        Track[] tracks = [T("a", name: "Track 1"), T("b", name: "Track 3")];

        Assert.Equal("Track 2", GroupRules.NextTrackName(tracks, "Track"));
        Assert.Equal("Bus 1", GroupRules.NextTrackName(tracks, "Bus"));
        Assert.Equal("Group 1", GroupRules.UniqueName([]));
        Assert.Equal("Group 3", GroupRules.UniqueName([G(_g1, "Group 2")]));
    }
}
