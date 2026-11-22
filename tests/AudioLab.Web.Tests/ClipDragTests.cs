using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the clip drag tests.</summary>
public sealed class ClipDragTests
{
    private readonly ProjectDocument _song = Song.Empty().With(
        Song.NewTrack("t1", 64, Song.Audio("a", 8, 4)),
        Song.WithMidi("keys", Song.Midi("m", 0, 4)),
        Song.NewTrack("t3", 64));

    private ClipDrag Press(string clip, double seconds, double row)
    {
        TrackLayout layout = TrackLayout.Of(_song);
        TimelineGeometry geometry = TimelineGeometry.Default;
        return new ClipDrag(_song, ClipId.From(clip), geometry, layout, geometry.X(seconds) + 1, (row * 64) + 30);
    }

    /// <summary>A press that never travels is a click and sends nothing.</summary>
    [Fact]
    public void APressThatNeverTravelsIsAClickAndSendsNothing()
    {
        ClipDrag click = Press("a", 9, 0);

        click.Move((34 * 9) + 1 + 2, 31);

        Assert.False(click.Active);
        Assert.Null(click.Commit());
    }

    /// <summary>The clip follows the pointer and lands on the grid with the servers rule.</summary>
    [Fact]
    public void TheClipFollowsThePointerAndLandsOnTheGridWithTheServersRule()
    {
        ClipDrag drag = Press("a", 9, 0);

        drag.Move((34 * 12.1) + 1, 30);

        Assert.True(drag.Active);
        Assert.Equal(11.125, drag.TargetStart, 6);
        Assert.Equal(0, drag.TargetRow);
        Assert.Equal(new ClipMove(ClipId.From("a"), 11.125, null), drag.Commit());
    }

    /// <summary>Releasing where the clip began sends nothing so undo and redo are left as they were.</summary>
    [Fact]
    public void ReleasingWhereTheClipBeganSendsNothingSoUndoAndRedoAreLeftAsTheyWere()
    {
        ClipDrag drag = Press("a", 9, 0);

        drag.Move((34 * 12) + 1, 30);
        drag.Move((34 * 9) + 1, 31);

        Assert.True(drag.Active);
        Assert.Equal(8, drag.TargetStart);
        Assert.Null(drag.Commit());
    }

    /// <summary>An audio clip lands only on an audio track and stays on the last one it may use.</summary>
    [Fact]
    public void AnAudioClipLandsOnlyOnAnAudioTrackAndStaysOnTheLastOneItMayUse()
    {
        ClipDrag drag = Press("a", 9, 0);

        drag.Move(drag.Left + 10, (1 * 64) + 30);
        Assert.Equal(0, drag.TargetRow);

        drag.Move(drag.Left + 10, (2 * 64) + 30);
        Assert.Equal(2, drag.TargetRow);
        Assert.Equal(new ClipMove(ClipId.From("a"), drag.TargetStart, TrackId.From("t3")), drag.Commit());
    }

    /// <summary>A midi clip lands only on an instrument track.</summary>
    [Fact]
    public void AMidiClipLandsOnlyOnAnInstrumentTrack()
    {
        ClipDrag drag = Press("m", 1, 1);

        drag.Move(drag.Left + 10, 30);
        Assert.Equal(1, drag.TargetRow);
        drag.Move(drag.Left + 10, (2 * 64) + 30);
        Assert.Equal(1, drag.TargetRow);
    }

    /// <summary>The clip is never carried before the start of the project or below the last track.</summary>
    [Fact]
    public void TheClipIsNeverCarriedBeforeTheStartOfTheProjectOrBelowTheLastTrack()
    {
        ClipDrag drag = Press("a", 9, 0);

        drag.Move(-500, 5000);

        Assert.Equal(0, drag.TargetStart);
        Assert.Equal(0, drag.Left);
        Assert.True(drag.Top + drag.Height <= TrackLayout.Of(_song).Total);
    }

    /// <summary>A drag of a clip that is not in the document is refused at once.</summary>
    [Fact]
    public void ADragOfAClipThatIsNotInTheDocumentIsRefusedAtOnce()
    {
        Assert.Throws<ArgumentException>(() => Press("missing", 1, 0));
    }

    /// <summary>The keyboard nudges one grid step or to the next track of its kind.</summary>
    [Fact]
    public void TheKeyboardNudgesOneGridStepOrToTheNextTrackOfItsKind()
    {
        ClipMove? later = ClipMoves.Nudge(_song, ClipId.From("a"), "ArrowRight");
        ClipMove? earlier = ClipMoves.Nudge(_song, ClipId.From("a"), "ArrowLeft");

        Assert.Equal(8.125, later!.Value.Start, 6);
        Assert.Equal(7.875, earlier!.Value.Start, 6);
        Assert.Null(later.Value.Track);
        Assert.Equal(TrackId.From("t3"), ClipMoves.Nudge(_song, ClipId.From("a"), "ArrowDown")!.Value.Track);
        Assert.Null(ClipMoves.Nudge(_song, ClipId.From("a"), "ArrowUp"));
        Assert.Null(ClipMoves.Nudge(_song, ClipId.From("m"), "ArrowDown"));
        Assert.Null(ClipMoves.Nudge(_song, ClipId.From("m"), "ArrowLeft"));
        Assert.Null(ClipMoves.Nudge(_song, ClipId.From("a"), "x"));
    }
}
