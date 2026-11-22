using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class GroupCommandTests
{
    private readonly CommandRig _rig = new();

    private readonly TrackId _a;

    private readonly TrackId _b;

    private readonly TrackId _c;

    private readonly TrackId _d;

    public GroupCommandTests()
    {
        _a = Add("A");
        _b = Add("B");
        _c = Add("C");
        _d = Add("D");
    }

    private TrackId Add(string name) => TrackId.From(_rig.Created(new AddTrack { Project = _rig.Id, Name = name })[0]);

    private GroupId Group(params TrackId[] tracks) => GroupId.From(_rig.Created(new CreateGroup { Project = _rig.Id, Tracks = [.. tracks] })[0]);

    private string[] Order() => [.. _rig.Project.Tracks.Select(track => track.Name)];

    [Fact]
    public void AGroupGathersItsTracksAndTakesDefaultsAndAServerAllocatedId()
    {
        GroupId id = Group(_a, _c);

        TrackGroup group = Assert.Single(_rig.Project.Groups);
        Assert.Equal(id, group.Id);
        Assert.Equal(("Group 1", ProjectDefaults.TrackColor(0)), (group.Name, group.Color));
        Assert.Equal((false, true, false, false, 1.0), (group.Collapsed, group.Tint, group.Muted, group.Soloed, group.Volume));
        Assert.Equal(["A", "C", "B", "D"], Order());
        Assert.All(_rig.Project.Tracks.Where(track => track.Name is "A" or "C"), track => Assert.Equal(id, track.Group));
    }

    [Fact]
    public void ANameAndColourAreUsedAsGivenAndABlankNameGetsTheDefault()
    {
        _rig.Ok(new CreateGroup { Project = _rig.Id, Tracks = [_a], Name = " Band ", Color = "#112233" });
        _rig.Ok(new CreateGroup { Project = _rig.Id, Tracks = [_b], Name = " " });

        Assert.Equal([("Band", "#112233"), ("Group 2", ProjectDefaults.TrackColor(1))], _rig.Project.Groups.Select(group => (group.Name, group.Color)));
    }

    [Fact]
    public void GroupingNeedsRealTracksAndAtLeastOne()
    {
        _rig.RefusedWith(new CreateGroup { Project = _rig.Id, Tracks = [] }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new CreateGroup { Project = _rig.Id, Tracks = [_a, TrackId.From("trk_none")] }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new CreateGroup { Project = _rig.Id, Tracks = [_a], Name = new string('g', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new CreateGroup { Project = _rig.Id, Tracks = [_a], Color = "red" }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void RegroupingATrackLeavesNoEmptyGroupBehind()
    {
        Group(_a);
        GroupId second = Group(_a, _b);

        Assert.Equal([second], _rig.Project.Groups.Select(group => group.Id));
    }

    [Fact]
    public void TracksAreMovedIntoAnExistingGroupAndOutOfIt()
    {
        GroupId id = Group(_b);

        _rig.Ok(new AssignToGroup { Project = _rig.Id, Tracks = [_d], Group = id });
        Assert.Equal(["A", "B", "D", "C"], Order());
        _rig.Ok(new AssignToGroup { Project = _rig.Id, Tracks = [_b, _d] });

        Assert.Empty(_rig.Project.Groups);
        Assert.All(_rig.Project.Tracks, track => Assert.Null(track.Group));
    }

    [Fact]
    public void AssigningToAMissingGroupOrAnEmptyListIsRefused()
    {
        _rig.RefusedWith(new AssignToGroup { Project = _rig.Id, Tracks = [_a], Group = GroupId.From("grp_none") }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new AssignToGroup { Project = _rig.Id, Tracks = [] }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new AssignToGroup { Project = _rig.Id, Tracks = [TrackId.From("trk_none")] }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void UngroupingFreesEveryMemberAndRemovesTheGroup()
    {
        GroupId id = Group(_a, _c);
        ProjectDocument grouped = _rig.Project;

        _rig.Ok(new Ungroup { Project = _rig.Id, Group = id });

        Assert.Empty(_rig.Project.Groups);
        Assert.All(_rig.Project.Tracks, track => Assert.Null(track.Group));
        _rig.History.Undo();
        Assert.Equal(grouped, _rig.Project);
    }

    [Fact]
    public void AGroupIsRenamedRecolouredFoldedTintedMutedSoloedAndFaded()
    {
        GroupId id = Group(_a);

        _rig.Ok(new RenameGroup { Project = _rig.Id, Group = id, Name = " Drums " });
        _rig.Ok(new SetGroupColor { Project = _rig.Id, Group = id, Color = "#abc" });
        _rig.Ok(new SetGroupCollapsed { Project = _rig.Id, Group = id, Collapsed = true });
        _rig.Ok(new SetGroupTint { Project = _rig.Id, Group = id, Tint = false });
        _rig.Ok(new SetGroupMuted { Project = _rig.Id, Group = id, Muted = true });
        _rig.Ok(new SetGroupSoloed { Project = _rig.Id, Group = id, Soloed = true });
        _rig.Ok(new SetGroupVolume { Project = _rig.Id, Group = id, Volume = 0.5 });

        TrackGroup group = _rig.Project.Groups[0];
        Assert.Equal(("Drums", "#abc", true, false, true, true, 0.5), (group.Name, group.Color, group.Collapsed, group.Tint, group.Muted, group.Soloed, group.Volume));
    }

    [Fact]
    public void GroupEditsRefuseWhatTheRulesForbid()
    {
        GroupId id = Group(_a);

        _rig.RefusedWith(new RenameGroup { Project = _rig.Id, Group = id, Name = "  " }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetGroupColor { Project = _rig.Id, Group = id, Color = "blue" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetGroupVolume { Project = _rig.Id, Group = id, Volume = 17 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetGroupVolume { Project = _rig.Id, Group = id, Volume = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetGroupVolume { Project = _rig.Id, Group = id, Volume = double.NaN }, CommandErrorCode.Invalid);
    }

    [Fact]
    public void AnUnknownGroupIsNotFound()
    {
        GroupId none = GroupId.From("grp_none");

        _rig.RefusedWith(new Ungroup { Project = _rig.Id, Group = none }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new RenameGroup { Project = _rig.Id, Group = none, Name = "x" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetGroupColor { Project = _rig.Id, Group = none, Color = "#fff" }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetGroupCollapsed { Project = _rig.Id, Group = none, Collapsed = true }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new SetGroupVolume { Project = _rig.Id, Group = none, Volume = 1 }, CommandErrorCode.NotFound);
        _rig.RefusedWith(new MoveGroup { Project = _rig.Id, Group = none, Direction = MoveDirection.Up }, CommandErrorCode.NotFound);
    }

    [Fact]
    public void AWholeGroupMovesAndATrackInsideItStaysInside()
    {
        GroupId id = Group(_b, _c);
        int steps = _rig.History.UndoCount;

        _rig.Ok(new MoveGroup { Project = _rig.Id, Group = id, Direction = MoveDirection.Down });
        Assert.Equal(["A", "D", "B", "C"], Order());
        _rig.Ok(new MoveGroup { Project = _rig.Id, Group = id, Direction = MoveDirection.Down });
        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.Ok(new MoveTrack { Project = _rig.Id, Track = _b, Direction = MoveDirection.Down });
        _rig.Ok(new MoveTrack { Project = _rig.Id, Track = _b, Direction = MoveDirection.Down });

        Assert.Equal(["A", "D", "C", "B"], Order());
    }

    [Fact]
    public void ATrackOutsideStepsOverTheWholeGroup()
    {
        Group(_b, _c);

        _rig.Ok(new MoveTrack { Project = _rig.Id, Track = _a, Direction = MoveDirection.Down });

        Assert.Equal(["B", "C", "A", "D"], Order());
    }

    [Fact]
    public void ARedoneGroupKeepsItsId()
    {
        GroupId id = Group(_a);

        _rig.History.Undo();
        _rig.History.Redo();

        Assert.Equal(id, _rig.Project.Groups[0].Id);
    }
}
