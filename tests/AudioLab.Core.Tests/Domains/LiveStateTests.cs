using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class LiveStateTests
{
    private static readonly ClientId _client = ClientId.From("test-client");

    private static ProjectDocument Project(params (string Id, bool Armed, bool Monitor)[] tracks)
    {
        return BlankProject.Create() with
        {
            Tracks = [.. tracks.Select(track => BlankProject.Track(track.Id) with { Armed = track.Armed, Monitor = track.Monitor })],
        };
    }

    [Fact]
    public void AProjectOpensWithNothingArmedOrMonitoredAndOneThatAlreadyDoesIsReturnedAsItWas()
    {
        ProjectDocument saved = Project(("trk_1", true, false), ("trk_2", false, true), ("trk_3", false, false));
        ProjectDocument clean = Project(("trk_1", false, false));

        ProjectDocument opened = LiveState.Reset(saved);

        Assert.All(opened.Tracks, track => Assert.False(track.Armed || track.Monitor));
        Assert.Equal(["trk_1", "trk_2", "trk_3"], opened.Tracks.Select(track => track.Id.Value));
        Assert.Same(clean, LiveState.Reset(clean));
        Assert.Empty(ProjectValidator.Validate(opened));
    }

    [Fact]
    public void ADocumentTakenBackKeepsTheArmingOfNowAndATrackItBringsBackIsNotArmed()
    {
        ProjectDocument before = Project(("trk_1", false, false), ("trk_2", true, true));
        ProjectDocument now = Project(("trk_1", true, false), ("trk_3", true, true));

        ProjectDocument carried = LiveState.Carry(before, now);

        Assert.Equal([(true, false), (false, false)], carried.Tracks.Select(track => (track.Armed, track.Monitor)));
        Assert.Equal(["trk_1", "trk_2"], carried.Tracks.Select(track => track.Id.Value));
        Assert.Same(before, LiveState.Carry(before, before));
    }

    [Fact]
    public void ADragThatEndsWhereItBeganLeavesNoStepEvenWhenATrackWasArmedDuringIt()
    {
        CommandRig rig = new(BlankProject.Create() with { Tracks = [BlankProject.Track("trk_1"), BlankProject.Track("trk_2")] });
        TrackId first = TrackId.From("trk_1");
        GestureId drag = GestureId.From("drag");
        int steps = rig.History.UndoCount;

        rig.Run(new SetTrackHeight { Project = rig.Id, Track = first, Height = 150 }, drag);
        rig.Run(new SetTrackArmed { Project = rig.Id, Track = first, Armed = true }, drag);
        rig.Run(new SetTrackHeight { Project = rig.Id, Track = first, Height = 100 }, drag);
        rig.History.EndGesture(_client, drag);

        Assert.Equal(steps, rig.History.UndoCount);
        Assert.True(rig.Project.Tracks[0].Armed);
    }

    [Fact]
    public void ADragWithAnArmInTheMiddleIsStillOneStepAndUndoKeepsTheArm()
    {
        CommandRig rig = new(BlankProject.Create() with { Tracks = [BlankProject.Track("trk_1")] });
        TrackId first = TrackId.From("trk_1");
        GestureId drag = GestureId.From("drag");
        int steps = rig.History.UndoCount;

        rig.Run(new SetTrackHeight { Project = rig.Id, Track = first, Height = 150 }, drag);
        rig.Run(new SetTrackArmed { Project = rig.Id, Track = first, Armed = true }, drag);
        rig.Run(new SetTrackHeight { Project = rig.Id, Track = first, Height = 180 }, drag);
        rig.History.EndGesture(_client, drag);

        Assert.Equal(steps + 1, rig.History.UndoCount);
        rig.History.Undo();
        Assert.Equal(100.0, rig.Project.Tracks[0].View.Height);
        Assert.True(rig.Project.Tracks[0].Armed);
    }
}
