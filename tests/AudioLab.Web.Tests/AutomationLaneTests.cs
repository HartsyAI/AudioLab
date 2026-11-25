using System.Collections;
using System.Text.RegularExpressions;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the automation lane tests: the line as the engine plays it, its bounded size, its hit test and a drag's rule.</summary>
public sealed class AutomationLaneTests
{
    private const double Height = 40;

    private static readonly AutomationLanePlan _volume = new(AutomationParameter.Volume, TimelineGeometry.Default, Height);

    private static readonly AutomationLanePlan _pan = new(AutomationParameter.Pan, TimelineGeometry.Default, Height);

    /// <summary>Volume is drawn from 0 to the display maximum, and a value above it sits at the top (D25).</summary>
    [Fact]
    public void VolumeIsDrawnToItsDisplayMaximum()
    {
        Assert.Equal(AutomationRules.VolumeDisplayMax, _volume.Max);
        Assert.Equal(0, _volume.Y(AutomationRules.VolumeDisplayMax), 9);
        Assert.Equal(0, _volume.Y(16), 9);
        Assert.Equal(20, _volume.Y(1), 9);
    }

    /// <summary>The line is straight between points for every parameter, as the engine plays it (D35).</summary>
    [Fact]
    public void EveryParameterIsDrawnStraightBetweenItsPoints()
    {
        string volume = _volume.Path([new AutomationPoint(0, 1), new AutomationPoint(1, 0)], 100);
        string pan = _pan.Path([new AutomationPoint(0, 0.5), new AutomationPoint(1, -1)], 100);

        Assert.Equal("M0 20L34 40H100", volume);
        Assert.Equal("M0 10L34 40H100", pan);
        Assert.DoesNotContain("H34", pan);
    }

    /// <summary>The line's size is bounded by the lane's width: at most four segments a pixel column, however many points it holds.</summary>
    [Fact]
    public void ALaneOfThirtySixThousandPointsDrawsAtMostFourSegmentsAPixel()
    {
        const double Width = 12240;
        List<AutomationPoint> points = [];
        for (int i = 0; i < 36000; i++)
        {
            points.Add(new AutomationPoint(i * 0.01, (i % 17) * 0.1));
        }

        string path = _volume.Path(points, Width);
        int segments = Regex.Matches(path, "[LMH]").Count;

        Assert.True(segments <= (4 * Width) + 2, $"{segments} segments");
        Assert.True(path.Length < 40 * Width, $"{path.Length} characters");
    }

    /// <summary>A hit test reads a few points, not all of them, and finds the point under the pointer.</summary>
    [Fact]
    public void AHitTestFindsThePointUnderThePointerByCountedWork()
    {
        List<AutomationPoint> points = [];
        for (int i = 0; i < 36000; i++)
        {
            points.Add(new AutomationPoint(i * 0.01, 0.5));
        }
        CountingLane lane = new(points);

        int? hit = _volume.IndexAt(lane, _volume.X(12), _volume.Y(0.5));
        int? miss = _volume.IndexAt(lane, _volume.X(12.005), _volume.Y(0.5) + 50);

        Assert.Equal(1200, hit);
        Assert.Null(miss);
        Assert.True(lane.Reads < 1000, $"{lane.Reads} reads");
    }

    /// <summary>A move of a dragged point reads no other point; the release reads the lane once to find where the point lands.</summary>
    [Fact]
    public void AMoveOfADragReadsNoOtherPoint()
    {
        List<AutomationPoint> points = [];
        for (int i = 0; i < 36000; i++)
        {
            points.Add(new AutomationPoint(i * 0.01, 0.5));
        }
        CountingLane lane = new(points);
        AutomationDrag drag = new(TrackId.From("t1"), lane, 1200, _volume, _volume.X(12), _volume.Y(0.5));
        lane.Reads = 0;

        for (int i = 1; i <= 100; i++)
        {
            drag.Move(_volume.X(12) + i, _volume.Y(0.5) - i);
        }

        Assert.Equal(0, lane.Reads);
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

    /// <summary>A vertical drag changes the value and keeps the time exactly, so it sends the same time it began with.</summary>
    [Fact]
    public void AVerticalDragKeepsTheTimeItBeganWith()
    {
        IReadOnlyList<AutomationPoint> points = [new AutomationPoint(1, 0.5), new AutomationPoint(2, 0.2), new AutomationPoint(3, 0.8)];
        AutomationDrag drag = new(TrackId.From("t1"), points, 1, _volume, _volume.X(2), _volume.Y(0.2));

        drag.Move(_volume.X(2), _volume.Y(0.6));
        AutomationMoveTarget? target = drag.Commit();

        Assert.NotNull(target);
        Assert.Equal(2, target.Value.Time);
        Assert.Equal(0.6, target.Value.Value, 6);
    }

    /// <summary>A released drag sends the point's index as it was, its absolute target, and where the server's rule lands it.</summary>
    [Fact]
    public void AMovedPointLandsWhereTheServersRuleLandsIt()
    {
        IReadOnlyList<AutomationPoint> points = [new AutomationPoint(1, 0.5), new AutomationPoint(2, 0.2), new AutomationPoint(3, 0.8)];
        AutomationDrag drag = new(TrackId.From("t1"), points, 1, _volume, _volume.X(2), _volume.Y(0.2));

        drag.Move(_volume.X(2.5), _volume.Y(0.9));
        AutomationMoveTarget? target = drag.Commit();

        Assert.NotNull(target);
        Assert.Equal(1, target.Value.Index);
        Assert.Equal(2.5, target.Value.Time, 6);
        Assert.Equal(0.9, target.Value.Value, 6);
        Assert.Equal(AutomationRules.Move(points, 1, new AutomationPoint(2.5, 0.9)).Index, target.Value.LandsAt);
    }

    /// <summary>A lane's counted reads of its points, for the hit test and the move.</summary>
    private sealed class CountingLane(IReadOnlyList<AutomationPoint> inner) : IReadOnlyList<AutomationPoint>
    {
        public int Reads { get; set; }

        public int Count
        {
            get
            {
                Reads++;
                return inner.Count;
            }
        }

        public AutomationPoint this[int index]
        {
            get
            {
                Reads++;
                return inner[index];
            }
        }

        public IEnumerator<AutomationPoint> GetEnumerator()
        {
            for (int i = 0; i < inner.Count; i++)
            {
                Reads++;
                yield return inner[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
