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

    /// <summary>The placeholder waveform is the same for the same clip and different for another.</summary>
    [Fact]
    public void ThePlaceholderWaveformIsTheSameForTheSameClipAndDifferentForAnother()
    {
        float[] first = PlaceholderWaveform.Peaks("clp_a", 64);

        Assert.Equal(first, PlaceholderWaveform.Peaks("clp_a", 64));
        Assert.NotEqual(first, PlaceholderWaveform.Peaks("clp_b", 64));
        Assert.All(first, peak => Assert.InRange(peak, 0.03f, 1f));
    }

    /// <summary>The path cache builds each clips path once at a given size.</summary>
    [Fact]
    public void ThePathCacheBuildsEachClipsPathOnceAtAGivenSize()
    {
        WavePathCache cache = new();

        string path = cache.For("clp_a", 200);

        Assert.Same(path, cache.For("clp_a", 200));
        Assert.Equal(1, cache.Count);
        cache.For("clp_a", 400);
        Assert.Equal(2, cache.Count);
        Assert.StartsWith("M0,50 L0.0,", path, StringComparison.Ordinal);
    }

    /// <summary>Midi notes are lines in one path, within the clip, with the highest pitch at the top.</summary>
    [Fact]
    public void MidiNotesAreOneLineEachInOnePathWithTheHighestPitchAtTheTop()
    {
        MidiClip clip = Song.Midi(
            "m1",
            0,
            4,
            new MidiNote(new NoteId(1), 72, 0, 1, 90),
            new MidiNote(new NoteId(2), 60, 3, 4, 90));

        Assert.Equal("M750 90h250M0 10h250", MidiPreview.Path(clip));
        Assert.Equal(string.Empty, MidiPreview.Path(Song.Midi("m2", 0, 4)));
    }

    /// <summary>A clip of one pitch is drawn in the middle, and a very short note is a whole unit that square caps make three pixels.</summary>
    [Fact]
    public void AClipOfOnePitchIsDrawnInTheMiddleAndAVeryShortNoteStaysVisible()
    {
        MidiClip clip = Song.Midi("m1", 0, 100, new MidiNote(new NoteId(1), 60, 0, 0.02, 90));

        Assert.Equal("M0 50h1", MidiPreview.Path(clip));
        Assert.Contains("stroke-linecap: square;", RepoText.Read("src/AudioLab.Web/Components/Clip.razor.css"), StringComparison.Ordinal);
        Assert.Contains("stroke-width: 3px;", RepoText.Read("src/AudioLab.Web/Components/Clip.razor.css"), StringComparison.Ordinal);
    }
}
