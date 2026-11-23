using AudioLab.Core.Model;
using AudioLab.Web.Media;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of dragging a media file onto the lanes and of adding it with the keyboard.</summary>
public sealed class MediaDragTests
{
    private static readonly (double Left, double Top) _lanes = (300, 100);

    private readonly ProjectDocument _song = WithGrid(
        Song.Empty().With(
            Song.NewTrack("t1", 64, Song.Audio("a", 8, 4)),
            Song.WithMidi("keys", Song.Midi("m", 0, 4)),
            Song.NewTrack("t3", 64)),
        "1/4");

    private static ProjectDocument WithGrid(ProjectDocument document, string grid) => document with { View = document.View with { Grid = grid } };

    private static MediaSummary File(double duration = 2.5) =>
        new("med_1", "kick.wav", MediaKind.Audio, MediaFormat.Wav, SampleEncoding.Pcm16, duration, 44100, 2, 100, new string('0', 64), DateTimeOffset.UnixEpoch);

    private MediaDrag Press(ProjectDocument? document = null)
    {
        ProjectDocument shown = document ?? _song;
        return new MediaDrag(File(), shown, TimelineGeometry.Default, TrackLayout.Of(shown), 50, 700);
    }

    // The client point of a lane point, with the lane surface at its place on the page.
    private static (double X, double Y) At(double seconds, double row) => (_lanes.Left + (seconds * 34), _lanes.Top + (row * 64) + 30);

    /// <summary>A press that does not travel is a click: there is no drag, no target and nothing to add.</summary>
    [Fact]
    public void APressThatDoesNotTravelIsAClick()
    {
        MediaDrag drag = Press();

        drag.Move(51, 701, _lanes);

        Assert.False(drag.Active);
        Assert.Null(drag.Target);
        Assert.Null(drag.Commit());
    }

    /// <summary>Over an audio lane the file lands on the grid under the pointer, with the width of its whole duration.</summary>
    [Fact]
    public void OverAnAudioLaneTheFileLandsOnTheGridUnderThePointerWithTheWidthOfItsDuration()
    {
        MediaDrag drag = Press();
        (double x, double y) = At(5.7, 2);

        drag.Move(x, y, _lanes);

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
        MediaDrag drag = Press();

        drag.Move(_lanes.Left + 0.4, _lanes.Top + 30, _lanes);

        Assert.Equal(0, Assert.NotNull(drag.Target).Start);
    }

    /// <summary>After a tempo change the file snaps to the lines the lanes draw, as a dragged clip does.</summary>
    [Fact]
    public void AfterATempoChangeTheFileSnapsToTheDrawnGridLines()
    {
        ProjectDocument slowing = WithGrid(
            Song.Empty(120, [new TempoChange(TempoChangeId.From("t1"), 4.25, 60)]).With(Song.NewTrack("t1", 64)),
            "1/4");
        MediaDrag drag = new(File(), slowing, TimelineGeometry.Default, TrackLayout.Of(slowing), 50, 700);
        (double x, double y) = At(5.7, 0);

        drag.Move(x, y, _lanes);

        Assert.Equal(5.75, Assert.NotNull(drag.Target).Start, 6);
    }

    /// <summary>Over a lane the file does not fit it says why, and a release there adds nothing.</summary>
    [Fact]
    public void OverALaneTheFileDoesNotFitItSaysWhyAndAReleaseThereAddsNothing()
    {
        MediaDrag drag = Press();
        (double x, double y) = At(3, 1);

        drag.Move(x, y, _lanes);

        Assert.Null(drag.Target);
        Assert.Equal("\"kick.wav\" is audio and goes on an audio track; \"keys\" is an instrument track.", drag.Refusal);
        Assert.Null(drag.Commit());
    }

    /// <summary>Away from the lanes, or over the headers, there is no target and no reason.</summary>
    [Fact]
    public void AwayFromTheLanesOrOverTheHeadersThereIsNoTargetAndNoReason()
    {
        MediaDrag drag = Press();

        drag.Move(500, 700, null);
        Assert.Null(drag.Target);
        Assert.Null(drag.Refusal);
        drag.Move(_lanes.Left - 20, _lanes.Top + 30, _lanes);
        Assert.Null(drag.Target);
        drag.Move(_lanes.Left + 100, _lanes.Top - 10, _lanes);
        Assert.Null(drag.Target);
        Assert.Null(drag.Commit());
        Assert.True(drag.Active);
    }

    /// <summary>A target that was found and then left is gone: a release away from the lanes adds nothing.</summary>
    [Fact]
    public void ATargetThatWasFoundAndThenLeftIsGone()
    {
        MediaDrag drag = Press();
        (double x, double y) = At(5, 0);
        drag.Move(x, y, _lanes);
        Assert.NotNull(drag.Target);

        drag.Move(500, 700, null);

        Assert.Null(drag.Commit());
    }

    /// <summary>Ending a drag that another drag has replaced leaves the newer one going.</summary>
    [Fact]
    public void EndingADragThatAnotherHasReplacedLeavesTheNewerOneGoing()
    {
        MediaDragState state = new();
        MediaDrag first = Press();
        MediaDrag second = Press();
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

    /// <summary>The keyboard says why it cannot: no audio track, or an instrument track selected.</summary>
    [Fact]
    public void TheKeyboardSaysWhyItCannot()
    {
        ProjectDocument none = Song.Empty().With(Song.WithMidi("keys"));

        (MediaPlacement? noAudio, string? first) = ClipInsertion.For(none, File(), null, null, 0);
        (MediaPlacement? onKeys, string? second) = ClipInsertion.For(_song, File(), TrackId.From("keys"), null, 0);

        Assert.Null(noAudio);
        Assert.Equal("Add an audio track first; a file needs an audio track to go on.", first);
        Assert.Null(onKeys);
        Assert.Equal("\"keys\" is an instrument track and audio goes on an audio track; select an audio track.", second);
    }
}
