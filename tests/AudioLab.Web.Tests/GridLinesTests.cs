using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The floor and the lines of the grid that the piano roll draws and snaps to.</summary>
public sealed class GridLinesTests
{
    /// <summary>The floor is the line at or before a point: the cell it falls in.</summary>
    [Fact]
    public void TheFloorIsTheLineAtOrBeforeAPoint()
    {
        GridSnap grid = new(Song.Empty());

        Assert.Equal(0.125, grid.Floor(0.2), 9);
        Assert.Equal(0.25, grid.Floor(0.25), 9);
        Assert.Equal(0, grid.Floor(-1), 9);
    }

    /// <summary>After a change of tempo the lines are where the tempo map puts them, and they stop at the end and at the limit.</summary>
    [Fact]
    public void AfterAChangeOfTempoTheLinesAreWhereTheTempoMapPutsThem()
    {
        GridSnap grid = new(Song.Empty(120, [new TempoChange(TempoChangeId.From("t1"), 4, 60)]));

        double[] lines = [.. grid.Lines(3.5, 6.5, 100)];

        Assert.Equal(3.5, lines[0], 9);
        Assert.Equal(3.625, lines[1], 9);
        Assert.Equal(4, lines[4], 9);
        Assert.Equal(4.25, lines[5], 9);
        Assert.True(lines.All(line => line <= 6.5 + 1e-9));
        Assert.Equal(5, grid.Lines(0, 1000, 5).Count());
    }
}
