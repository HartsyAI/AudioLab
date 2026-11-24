using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>The lines under the notes of a clip that is very long: bounded, and still drawn to its end.</summary>
public sealed class RollGridLongClipTests
{
    private static int Lines(string path) => path.Count(letter => letter == 'M');

    private static double[] Positions(string path)
    {
        string[] lines = path.Split('M', StringSplitOptions.RemoveEmptyEntries);
        return [.. lines.Select(line => double.Parse(line.Split(' ')[0], CultureInfo.InvariantCulture))];
    }

    /// <summary>A kind of line that would be more than the limit is left out, and bar lines skip bars, so a long clip has a bounded path.</summary>
    [Fact]
    public void ALongClipIsDrawnToItsEndWithABoundedPath()
    {
        MidiClip clip = Song.Midi("m", 0, 40_000);
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", clip));

        RollLines lines = RollGrid.Lines(document, clip, 80);

        Assert.True(Lines(lines.Bars) <= RollGrid.LineLimit);
        Assert.Equal(0, Lines(lines.Steps));
        Assert.Equal(0, Lines(lines.Beats));
        double[] bars = Positions(lines.Bars);
        Assert.True(bars[^1] > 39_000, $"The last bar line is at {bars[^1]}.");
        Assert.True(bars[1] - bars[0] >= 6 / 80.0);
    }

    /// <summary>A clip of ordinary length is not thinned.</summary>
    [Fact]
    public void AClipOfOrdinaryLengthKeepsEveryBar()
    {
        MidiClip clip = Song.Midi("m", 0, 120);
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", clip));

        RollLines lines = RollGrid.Lines(document, clip, 80);

        Assert.Equal(61, Lines(lines.Bars));
        Assert.Equal(2, Positions(lines.Bars)[1] - Positions(lines.Bars)[0]);
    }
}
