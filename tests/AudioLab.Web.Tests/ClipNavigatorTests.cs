using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the clip navigator tests.</summary>
public sealed class ClipNavigatorTests
{
    private readonly ProjectDocument _song = Song.Empty().With(
        Song.NewTrack("t1", 64, Song.Audio("a", 0, 4), Song.Audio("b", 8, 4)),
        Song.NewTrack("t2"),
        Song.NewTrack("t3", 64, Song.Audio("c", 2, 2), Song.Audio("d", 10, 4)));

    private ClipId? Press(string from, string key) => ClipNavigator.Next(_song, ClipId.From(from), key);

    /// <summary>Left and right walk the clips of a track in time order and stop at the ends.</summary>
    [Fact]
    public void LeftAndRightWalkTheClipsOfATrackInTimeOrderAndStopAtTheEnds()
    {
        Assert.Equal(ClipId.From("b"), Press("a", "ArrowRight"));
        Assert.Equal(ClipId.From("a"), Press("b", "ArrowLeft"));
        Assert.Null(Press("a", "ArrowLeft"));
        Assert.Null(Press("b", "ArrowRight"));
    }

    /// <summary>Home and end jump to the first and last clip of the track.</summary>
    [Fact]
    public void HomeAndEndJumpToTheFirstAndLastClipOfTheTrack()
    {
        Assert.Equal(ClipId.From("b"), Press("a", "End"));
        Assert.Equal(ClipId.From("a"), Press("b", "Home"));
        Assert.Null(Press("a", "Home"));
    }

    /// <summary>Up and down skip an empty track and land on the clip under the start or the nearest one.</summary>
    [Fact]
    public void UpAndDownSkipAnEmptyTrackAndLandOnTheClipUnderTheStartOrTheNearestOne()
    {
        Assert.Equal(ClipId.From("c"), Press("a", "ArrowDown"));
        Assert.Equal(ClipId.From("d"), Press("b", "ArrowDown"));
        Assert.Equal(ClipId.From("a"), Press("c", "ArrowUp"));
        Assert.Null(Press("a", "ArrowUp"));
        Assert.Null(Press("c", "ArrowDown"));
    }

    /// <summary>An unknown key or clip moves nowhere and the first clip is where the tab stop starts.</summary>
    [Fact]
    public void AnUnknownKeyOrClipMovesNowhereAndTheFirstClipIsWhereTheTabStopStarts()
    {
        Assert.Null(Press("a", "x"));
        Assert.Null(Press("missing", "ArrowRight"));
        Assert.Equal(ClipId.From("a"), ClipNavigator.First(_song));
        Assert.Null(ClipNavigator.First(Song.Empty()));
    }

    /// <summary>A midi clip is part of its tracks order.</summary>
    [Fact]
    public void AMidiClipIsPartOfItsTracksOrder()
    {
        ProjectDocument document = Song.Empty().With(Song.WithMidi("k", Song.Midi("m1", 4, 2), Song.Midi("m0", 0, 2)));

        Assert.Equal(ClipId.From("m1"), ClipNavigator.Next(document, ClipId.From("m0"), "ArrowRight"));
    }

    /// <summary>A selection of something that was deleted is dropped.</summary>
    [Fact]
    public void ASelectionOfSomethingThatWasDeletedIsDropped()
    {
        SessionSelection selection = new();
        selection.SelectClip(TrackId.From("t1"), ClipId.From("a"));
        int changes = 0;
        selection.Changed += () => changes++;
        ProjectDocument withoutClip = Song.Empty().With(Song.NewTrack("t1", 64, Song.Audio("b", 8, 4)));

        selection.Prune(withoutClip);
        Assert.Equal(TrackId.From("t1"), selection.Track);
        Assert.Null(selection.Clip);

        selection.Prune(Song.Empty());
        Assert.Null(selection.Track);
        Assert.Equal(2, changes);
    }
}
