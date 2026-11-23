using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Where a new MIDI clip goes and how a clip's length steps: through the tempo map, never by bar arithmetic of the page's own.</summary>
public sealed class MidiClipPlanTests
{
    private static ProjectDocument Doc(params MidiClip[] clips) => Song.Empty().With(Song.WithMidi("keys", clips));

    /// <summary>A new clip is one bar long at the tempo where it starts, also after a tempo change.</summary>
    [Fact]
    public void ANewClipIsOneBarAtTheTempoWhereItStarts()
    {
        ProjectDocument fast = Song.Empty(120, [new TempoChange(TempoChangeId.From("t1"), 8, 240)]);

        Assert.Equal(2, MidiClipPlan.DefaultLength(Song.Empty(), 0), 9);
        Assert.Equal(1, MidiClipPlan.DefaultLength(fast, 8), 9);
    }

    /// <summary>With no pointer a clip goes on the first bar line after the track's last clip, and at 0 on an empty track.</summary>
    [Fact]
    public void AClipWithNoPlaceGoesOnTheFirstBarLineAfterTheLastClip()
    {
        ProjectDocument empty = Doc();
        ProjectDocument held = Doc(Song.Midi("a", 1, 2.5), Song.Midi("b", 0, 1));

        Assert.Equal(0, MidiClipPlan.NextStart(empty, empty.Tracks[0]));
        Assert.Equal(4, MidiClipPlan.NextStart(held, held.Tracks[0]), 9);
        ProjectDocument onTheLine = Doc(Song.Midi("a", 0, 4));
        Assert.Equal(4, MidiClipPlan.NextStart(onTheLine, onTheLine.Tracks[0]), 9);
    }

    /// <summary>A step moves the end by whole bars, and a clip is never stepped below the least length.</summary>
    [Fact]
    public void AStepMovesTheEndByWholeBarsAndNeverBelowTheLeastLength()
    {
        ProjectDocument document = Doc(Song.Midi("a", 0, 4));
        MidiClip clip = document.Tracks[0].MidiClips[0];

        Assert.Equal(6, MidiClipPlan.Stepped(document, clip, 1)!.Value, 9);
        Assert.Equal(2, MidiClipPlan.Stepped(document, clip, -1)!.Value, 9);
        Assert.Null(MidiClipPlan.Stepped(document, clip, -2));
        Assert.Equal(2, MidiClipPlan.Bars(document, clip), 9);
    }

    /// <summary>A clip is found by id with its track.</summary>
    [Fact]
    public void AClipIsFoundByIdWithItsTrack()
    {
        ProjectDocument document = Doc(Song.Midi("a", 0, 4));

        Assert.Equal("keys", MidiClipPlan.Find(document, ClipId.From("a"))!.Value.Track.Name);
        Assert.Null(MidiClipPlan.Find(document, ClipId.From("missing")));
    }
}
