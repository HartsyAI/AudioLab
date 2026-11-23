using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The one snap rule of a drag and a click: lines counted in beats through the tempo map, or in seconds.</summary>
public sealed class GridSnapTests
{
    /// <summary>At 120 beats a minute the grid of sixteenths is 0.125 s, and a point goes to the nearest line, never before the start.</summary>
    [Fact]
    public void ASixteenthIsAnEighthOfASecondAtOneHundredAndTwentyBeats()
    {
        GridSnap grid = new(Song.Empty());

        Assert.Equal(0.125, grid.Step(0), 9);
        Assert.Equal(0.25, grid.Snap(0.2), 9);
        Assert.Equal(0, grid.Snap(0.05), 9);
        Assert.Equal(0, grid.Snap(-3), 9);
    }

    /// <summary>After a change of tempo the lines are where the tempo map puts them, so they stay on their bars.</summary>
    [Fact]
    public void AfterAChangeOfTempoTheLinesStayOnTheirBars()
    {
        GridSnap grid = new(Song.Empty(120, [new TempoChange(TempoChangeId.From("t1"), 4, 60)]));

        Assert.Equal(0.125, grid.Step(3), 9);
        Assert.Equal(0.25, grid.Step(5), 9);
        Assert.Equal(4.25, grid.Snap(4.3), 9);
        Assert.Equal(4, grid.Snap(3.99), 9);
        Assert.Equal(6, grid.Snap(5.97), 9);
    }
}
