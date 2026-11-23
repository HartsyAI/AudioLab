using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>The lines under the notes come from the project's tempo map, as the arrangement's do.</summary>
public sealed class RollGridTests
{
    private static int Lines(string path) => path.Count(letter => letter == 'M');

    /// <summary>A bar, its beats and its grid steps are drawn apart, and steps too close to read are left out.</summary>
    [Fact]
    public void BarsBeatsAndStepsAreDrawnApartAndStepsTooCloseAreLeftOut()
    {
        MidiClip clip = Song.Midi("m", 0, 4);
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", clip));

        RollLines close = RollGrid.Lines(document, clip, 80);
        RollLines far = RollGrid.Lines(document, clip, 20);

        Assert.Equal(3, Lines(close.Bars));
        Assert.Equal(6, Lines(close.Beats));
        Assert.Equal(33, Lines(close.Steps));
        Assert.Equal(0, Lines(far.Steps));
    }

    /// <summary>A clip that starts off the bar line has its bar lines where the tempo map puts them.</summary>
    [Fact]
    public void ABarLineFallsWhereTheTempoMapPutsItInsideAClipThatStartsOffTheLine()
    {
        MidiClip clip = Song.Midi("m", 1, 4);
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", clip));

        RollLines lines = RollGrid.Lines(document, clip, 80);

        Assert.StartsWith("M1 0V128", lines.Bars, StringComparison.Ordinal);
        Assert.Contains("M3 0V128", lines.Bars, StringComparison.Ordinal);
    }

    /// <summary>The black keys are bands and each C starts an octave line.</summary>
    [Fact]
    public void TheBlackKeysAreBandsAndEveryCStartsAnOctaveLine()
    {
        (string black, string octaves) = RollGrid.Rows(4);

        Assert.Equal(53, Lines(black));
        Assert.Equal(11, Lines(octaves));
        Assert.True(RollGrid.IsBlack(61));
        Assert.False(RollGrid.IsBlack(60));
    }
}
