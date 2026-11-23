using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the drawing rule tests.</summary>
public sealed class DrawingRuleTests
{
    private static readonly string[] _tokens =
    [
        "#ee6d86", "#ef7d67", "#ee9a55", "#e3ad4f", "#a9cf5b", "#5fcf95",
        "#4fcdb8", "#4fbfdc", "#6c93f2", "#a487ee", "#cf7de0", "#e07ab5",
    ];

    /// <summary>Each of the twelve track colours maps to itself.</summary>
    [Fact]
    public void EachOfTheTwelveTrackColoursMapsToItself()
    {
        TrackColorMap map = new(_tokens);

        for (int i = 0; i < _tokens.Length; i++)
        {
            Assert.Equal(i + 1, map.Nearest(_tokens[i]));
        }
    }

    /// <summary>Another colour takes the nearest track colour and an unreadable one the first.</summary>
    [Fact]
    public void AnotherColourTakesTheNearestTrackColourAndAnUnreadableOneTheFirst()
    {
        TrackColorMap map = new(_tokens);

        Assert.Equal("var(--track-9)", map.Token("#4a9eff"));
        Assert.Equal(9, map.Nearest("#4A9EFF80"));
        Assert.Equal(2, map.Nearest("#f00"));
        Assert.Equal(1, map.Nearest("not a colour"));
        Assert.Equal(1, map.Nearest(null));
    }

    /// <summary>The map needs twelve readable colours.</summary>
    [Fact]
    public void TheMapNeedsTwelveReadableColours()
    {
        Assert.Throws<ArgumentException>(() => new TrackColorMap(_tokens[..11]));
        Assert.Throws<ArgumentException>(() => new TrackColorMap([.. _tokens[..11], "rgb(1, 2, 3)"]));
    }

    /// <summary>The fader and the header slider share one taper.</summary>
    [Theory]
    [InlineData(1.0, 0.75, "0.0")]
    [InlineData(0.5011872336, 0.625, "-6.0")]
    [InlineData(0.0, 0.0, "-inf")]
    [InlineData(16.0, 1.0, "+24.1")]
    [InlineData(0.0039810717, 0.0, "-48.0")]
    public void TheFaderAndTheHeaderSliderShareOneTaper(double gain, double position, string text)
    {
        Assert.Equal(position, FaderScale.Position(gain), 3);
        Assert.Equal(text, FaderScale.Text(gain));
    }

    /// <summary>Midi notes are drawn within the clip with the highest pitch at the top.</summary>
    [Fact]
    public void MidiNotesAreDrawnWithinTheClipWithTheHighestPitchAtTheTop()
    {
        MidiClip clip = Song.Midi(
            "m1",
            0,
            4,
            new MidiNote(new NoteId(1), 72, 0, 1, 90),
            new MidiNote(new NoteId(2), 60, 3, 4, 90));

        NoteBox[] boxes = MidiNoteLayout.Boxes(clip);

        Assert.Equal(new NoteBox(0, 10, 25), boxes[0]);
        Assert.Equal(new NoteBox(75, 90, 25), boxes[1]);
        Assert.Empty(MidiNoteLayout.Boxes(Song.Midi("m2", 0, 4)));
    }
}
