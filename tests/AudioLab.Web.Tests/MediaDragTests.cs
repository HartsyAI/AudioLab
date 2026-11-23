using AudioLab.Core.Model;
using AudioLab.Web.Media;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of dragging a media file onto the lanes and of where the keyboard adds it.</summary>
public sealed class MediaDragTests
{
    private static readonly (double Left, double Top) _lanes = (300, 100);

    private static readonly TimelineGeometry _geometry = TimelineGeometry.Default;

    private readonly ProjectDocument _song = WithGrid(
        Song.Empty().With(
            Song.NewTrack("t1", 64, Song.Audio("a", 8, 4)),
            Song.WithMidi("keys", Song.Midi("m", 0, 4)),
            Song.NewTrack("t3", 64)),
        "1/4");

    private static ProjectDocument WithGrid(ProjectDocument document, string grid) => document with { View = document.View with { Grid = grid } };

    private static MediaSummary File(double duration = 2.5) => Song.Media("med_1", "kick.wav", duration);

    // The client point of a lane point, with the lane surface at its place on the page.
    private static (double X, double Y) At(double seconds, double row) => (_lanes.Left + (seconds * 34), _lanes.Top + (row * 64) + 30);

    private void Move(MediaDrag drag, (double X, double Y) at, ProjectDocument? document = null)
    {
        drag.Move(at.X, at.Y, _lanes, document ?? _song, _geometry);
    }

    /// <summary>A press that does not travel is a click: there is no drag, no target and nothing to add.</summary>
    [Fact]
    public void APressThatDoesNotTravelIsAClick()
    {
        MediaDrag drag = new(File(), 50, 700);

        drag.Move(51, 701, _lanes, _song, _geometry);

        Assert.False(drag.Active);
        Assert.Null(drag.Target);
        Assert.Null(drag.Commit());
    }

    /// <summary>Over an audio lane the file lands on the grid under the pointer, with the width of its whole duration.</summary>
    [Fact]
    public void OverAnAudioLaneTheFileLandsOnTheGridUnderThePointerWithTheWidthOfItsDuration()
    {
        MediaDrag drag = new(File(), 50, 700);

        Move(drag, At(5.7, 2));

        MediaTarget target = Assert.NotNull(drag.Target);
        Assert.Equal(2, target.Row);
        Assert.Equal(5.5, target.Start, 6);
        Assert.Equal((5.5 * 34) + 1, target.Left, 6);
        Assert.Equal((2 * 64) + 4, target.Top);
        Assert.Equal((2.5 * 34) - 2, target.Width, 6);
        Assert.Equal(64 - 9, target.Height);
        MediaPlacement placement = Assert.NotNull(drag.Commit());
        Assert.Equal((TrackId.From("t3"), 5.5), (placement.Track, placement.Start));
        Assert.Equal("med_1", placement.File.Id);
        Assert.Null(drag.Refusal);
    }

    /// <summary>The start is never before the start of the project.</summary>
    [Fact]
    public void TheStartIsNeverBeforeTheStartOfTheProject()
    {
        MediaDrag drag = new(File(), 50, 700);

        drag.Move(_lanes.Left + 0.4, _lanes.Top + 30, _lanes, _song, _geometry);

        Assert.Equal(0, Assert.NotNull(drag.Target).Start);
    }

    /// <summary>The zoom that is given is the zoom that is used: at twice the scale the same pointer is half the time.</summary>
    [Fact]
    public void TheZoomThatIsGivenIsTheZoomThatIsUsed()
    {
        MediaDrag drag = new(File(), 50, 700);
        (double x, double y) = At(10, 0);

        drag.Move(x, y, _lanes, _song, new TimelineGeometry(68));

        Assert.Equal(5.0, Assert.NotNull(drag.Target).Start, 6);
        Assert.Equal((5.0 * 68) + 1, Assert.NotNull(drag.Target).Left, 6);
    }

    /// <summary>After a tempo change the file snaps to the lines the lanes draw, as a dragged clip does.</summary>
    [Fact]
    public void AfterATempoChangeTheFileSnapsToTheDrawnGridLines()
    {
        ProjectDocument slowing = WithGrid(
            Song.Empty(120, [new TempoChange(TempoChangeId.From("t1"), 4.25, 60)]).With(Song.NewTrack("t1", 64)),
            "1/4");
        MediaDrag drag = new(File(), 50, 700);

        Move(drag, At(5.7, 0), slowing);

        Assert.Equal(5.75, Assert.NotNull(drag.Target).Start, 6);
    }

    /// <summary>A track that another client added during the drag is the one the release lands on: the document is read at every move.</summary>
    [Fact]
    public void ATrackAddedDuringTheDragIsTheTrackTheReleaseLandsOn()
    {
        ProjectDocument before = Song.Empty().With(Song.NewTrack("t1", 64), Song.NewTrack("t2", 64));
        ProjectDocument after = Song.Empty().With(Song.NewTrack("new", 64), Song.NewTrack("t1", 64), Song.NewTrack("t2", 64));
        MediaDrag drag = new(File(), 50, 700);

        Move(drag, At(3, 1), before);
        Assert.Equal(TrackId.From("t2"), Assert.NotNull(drag.Commit()).Track);
        Move(drag, At(3, 1), after);

        Assert.Equal(TrackId.From("t1"), Assert.NotNull(drag.Commit()).Track);
    }

    /// <summary>Over a lane the file does not fit it says why, and a release there adds nothing.</summary>
    [Fact]
    public void OverALaneTheFileDoesNotFitItSaysWhyAndAReleaseThereAddsNothing()
    {
        MediaDrag drag = new(File(), 50, 700);

        Move(drag, At(3, 1));

        Assert.Null(drag.Target);
        Assert.Equal("\"kick.wav\" is audio and goes on an audio track; \"keys\" is an instrument track.", drag.Refusal);
        Assert.Null(drag.Commit());
    }

    /// <summary>Away from the lanes, over the headers or rulers, or below the last track there is no target, no reason and nothing to add.</summary>
    [Fact]
    public void AwayFromTheLanesOrBelowTheLastTrackThereIsNoTargetAndNoReason()
    {
        MediaDrag drag = new(File(), 50, 700);

        drag.Move(500, 700, null, _song, _geometry);
        Assert.Null(drag.Target);
        Assert.Null(drag.Refusal);
        drag.Move(_lanes.Left - 20, _lanes.Top + 30, _lanes, _song, _geometry);
        Assert.Null(drag.Target);
        drag.Move(_lanes.Left + 100, _lanes.Top - 10, _lanes, _song, _geometry);
        Assert.Null(drag.Target);
        drag.Move(_lanes.Left + 100, _lanes.Top + (3 * 64) + 5, _lanes, _song, _geometry);
        Assert.Null(drag.Target);
        Assert.Null(drag.Refusal);
        Assert.Null(drag.Commit());
        Assert.True(drag.Active);
    }

    /// <summary>A target that was found and then left is gone: a release away from the lanes adds nothing.</summary>
    [Fact]
    public void ATargetThatWasFoundAndThenLeftIsGone()
    {
        MediaDrag drag = new(File(), 50, 700);
        Move(drag, At(5, 0));
        Assert.NotNull(drag.Target);

        drag.Move(500, 700, null, _song, _geometry);

        Assert.Null(drag.Commit());
    }

    /// <summary>Where the project cannot be edited the drag follows the pointer and says so, and a release adds nothing.</summary>
    [Fact]
    public void WhereTheProjectCannotBeEditedTheDragSaysSo()
    {
        MediaDrag drag = new(File(), 50, 700, "Reconnecting to the server.");

        Move(drag, At(5, 0));

        Assert.Null(drag.Target);
        Assert.Equal("kick.wav cannot be added now. Reconnecting to the server.", drag.Refusal);
        Assert.Null(drag.Commit());
    }

    /// <summary>Ending a drag that another drag has replaced leaves the newer one going.</summary>
    [Fact]
    public void EndingADragThatAnotherHasReplacedLeavesTheNewerOneGoing()
    {
        MediaDragState state = new();
        MediaDrag first = new(File(), 50, 700);
        MediaDrag second = new(File(), 50, 700);
        state.Begin(first);
        state.Begin(second);

        state.End(first);
        Assert.Same(second, state.Current);
        state.End(second);

        Assert.Null(state.Current);
    }

    /// <summary>The keyboard adds on the selected audio track at the playhead, or after the selected clip.</summary>
    [Fact]
    public void TheKeyboardAddsOnTheSelectedAudioTrackAtThePlayheadOrAfterTheSelectedClip()
    {
        (MediaPlacement? atPlayhead, string? none) = ClipInsertion.For(_song, File(), TrackId.From("t3"), null, 7.5);
        (MediaPlacement? afterClip, _) = ClipInsertion.For(_song, File(), TrackId.From("t1"), ClipId.From("a"), 7.5);
        (MediaPlacement? firstAudio, _) = ClipInsertion.For(_song, File(), null, null, 0);

        Assert.Null(none);
        Assert.Equal((TrackId.From("t3"), 7.5), (atPlayhead!.Value.Track, atPlayhead.Value.Start));
        Assert.Equal((TrackId.From("t1"), 12.0), (afterClip!.Value.Track, afterClip.Value.Start));
        Assert.Equal(TrackId.From("t1"), firstAudio!.Value.Track);
    }

    /// <summary>A selected track that is gone falls back to the first audio track; the keyboard says why when it really cannot add.</summary>
    [Fact]
    public void ASelectedTrackThatIsGoneFallsBackAndTheKeyboardSaysWhyItCannot()
    {
        ProjectDocument none = Song.Empty().With(Song.WithMidi("keys"));

        (MediaPlacement? fallback, _) = ClipInsertion.For(_song, File(), TrackId.From("gone"), null, 2);
        (MediaPlacement? noAudio, string? first) = ClipInsertion.For(none, File(), null, null, 0);
        (MediaPlacement? onKeys, string? second) = ClipInsertion.For(_song, File(), TrackId.From("keys"), null, 0);

        Assert.Equal((TrackId.From("t1"), 2.0), (fallback!.Value.Track, fallback.Value.Start));
        Assert.Null(noAudio);
        Assert.Equal("Add an audio track first; a file needs an audio track to go on.", first);
        Assert.Null(onKeys);
        Assert.Equal("\"keys\" is an instrument track and audio goes on an audio track; select an audio track.", second);
    }
}
