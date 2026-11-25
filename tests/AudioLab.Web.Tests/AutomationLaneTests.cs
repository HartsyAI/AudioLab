using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the automation lane tests: the line a lane draws, and the rule a point drag sends.</summary>
public sealed class AutomationLaneTests
{
    private const double Height = 40;

    private static readonly AutomationLanePlan _volume = new(AutomationParameter.Volume, TimelineGeometry.Default, Height);

    private static readonly AutomationLanePlan _pan = new(AutomationParameter.Pan, TimelineGeometry.Default, Height);

    /// <summary>Volume runs straight from one point to the next, as the engine reads it (D35).</summary>
    [Fact]
    public void VolumeRunsStraightBetweenItsPoints()
    {
        string path = _volume.Path([new AutomationPoint(0, 1), new AutomationPoint(1, 0)], 100);

        Assert.Equal("M0 37.5L0 37.5L34 40H100", path);
    }

    /// <summary>Pan holds each point's value until the next point, so its line has steps (D35).</summary>
    [Fact]
    public void PanHoldsEachValueUntilTheNextPoint()
    {
        string path = _pan.Path([new AutomationPoint(0, 0.5), new AutomationPoint(1, -1)], 100);

        Assert.Equal("M0 10H0L0 10H34L34 40H100", path);
    }

    /// <summary>A press that does not travel the threshold, or that comes back to where it began, sends nothing.</summary>
    [Fact]
    public void AClickOrAReturnToWhereItBeganSendsNothing()
    {
        IReadOnlyList<AutomationPoint> points = [new AutomationPoint(1, 0.5)];
        double startX = _volume.X(1);
        double startY = _volume.Y(0.5);
        AutomationDrag click = new(TrackId.From("t1"), points, 0, _volume, startX, startY);
        click.Move(startX + 2, startY);
        Assert.Null(click.Commit());

        AutomationDrag back = new(TrackId.From("t1"), points, 0, _volume, startX, startY);
        back.Move(startX + 30, startY);
        back.Move(startX, startY);
        Assert.Null(back.Commit());
    }

    /// <summary>A released drag sends the point's index as it was and its absolute target, which is the rule's landing index.</summary>
    [Fact]
    public void AMovedPointSendsItsIndexAndAbsoluteTarget()
    {
        IReadOnlyList<AutomationPoint> points = [new AutomationPoint(1, 0.5)];
        AutomationDrag drag = new(TrackId.From("t1"), points, 0, _volume, _volume.X(1), _volume.Y(0.5));

        drag.Move(_volume.X(2), _volume.Y(4));
        AutomationMoveTarget? target = drag.Commit();

        Assert.NotNull(target);
        Assert.Equal(0, target.Value.Index);
        Assert.Equal(2, target.Value.Time, 6);
        Assert.Equal(4, target.Value.Value, 6);
        Assert.Equal(0, target.Value.LandsAt);
    }
}
