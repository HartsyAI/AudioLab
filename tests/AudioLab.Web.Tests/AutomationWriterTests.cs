using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the write pass tests: what each mode records from the transport's ticks and the control's values (D69).</summary>
public sealed class AutomationWriterTests
{
    /// <summary>Write records every sample from the start of the pass to its stop, in one segment.</summary>
    [Fact]
    public void WriteRecordsEverySampleFromBeginToStop()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Volume);

        writer.Begin(0, 1);
        writer.Sample(1, 1.5);
        writer.Sample(2, 2);
        AutomationPass? pass = writer.Stop(3, 2.5);

        Assert.NotNull(pass);
        Assert.Single(pass.Value.Segments);
        Assert.Equal(new double[] { 0, 1, 2, 3 }, pass.Value.Segments[0].Select(point => point.Time));
        Assert.Equal(new double[] { 1, 1.5, 2, 2.5 }, pass.Value.Segments[0].Select(point => point.Value));
        Assert.False(pass.Value.ReturnToOld);
    }

    /// <summary>Touch records only while held, each touch as its own segment that hands the lane back to the old curve.</summary>
    [Fact]
    public void TouchRecordsOnlyWhileHeldAndEachTouchIsItsOwnSegment()
    {
        AutomationWriter writer = new(AutomationMode.Touch, AutomationParameter.Pan);

        writer.Begin(0, 0.1);
        writer.Sample(0.5, 0.9);
        writer.Grab(1, 0.2);
        writer.Sample(1.5, 0.3);
        writer.Release(2, 0.4);
        writer.Sample(2.5, -0.9);
        writer.Grab(3, 0.7);
        writer.Release(4, 0.8);
        AutomationPass? pass = writer.Stop(5, 0.0);

        Assert.NotNull(pass);
        Assert.Equal(2, pass.Value.Segments.Count);
        Assert.Equal(new double[] { 1, 1.5, 2 }, pass.Value.Segments[0].Select(point => point.Time));
        Assert.Equal(new double[] { 3, 4 }, pass.Value.Segments[1].Select(point => point.Time));
        Assert.True(pass.Value.ReturnToOld);
    }

    /// <summary>Latch records from the first touch to the stop, in one segment, and keeps the value where it was left.</summary>
    [Fact]
    public void LatchRecordsFromTheFirstTouchToTheStop()
    {
        AutomationWriter writer = new(AutomationMode.Latch, AutomationParameter.Volume);

        writer.Begin(0, 1);
        writer.Sample(0.5, 5);
        writer.Grab(1, 2);
        writer.Release(2, 3);
        writer.Sample(3, 4);
        AutomationPass? pass = writer.Stop(4, 4);

        Assert.NotNull(pass);
        Assert.Single(pass.Value.Segments);
        Assert.Equal(new double[] { 1, 2, 3, 4 }, pass.Value.Segments[0].Select(point => point.Time));
        Assert.False(pass.Value.ReturnToOld);
    }

    /// <summary>A pass that was never touched in touch writes nothing, so it has nothing to commit.</summary>
    [Fact]
    public void APassThatWroteNothingHasNothingToCommit()
    {
        AutomationWriter writer = new(AutomationMode.Touch, AutomationParameter.Volume);

        writer.Begin(0, 1);
        writer.Sample(1, 2);

        Assert.Null(writer.Stop(2, 2));
    }

    /// <summary>Samples before the pass begins are not recorded, and a pass is recorded only in write, touch and latch.</summary>
    [Fact]
    public void SamplesOutsideAPassAreNotRecorded()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Volume);

        writer.Sample(0, 1);
        Assert.False(writer.Recording);
        Assert.Null(writer.Stop(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationWriter(AutomationMode.Read, AutomationParameter.Volume));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationWriter(AutomationMode.Off, AutomationParameter.Pan));
    }

    /// <summary>A tick that goes back in time, as a loop or a seek back does, is dropped, so two laps never interleave.</summary>
    [Fact]
    public void ATickThatGoesBackInTimeIsDropped()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Volume);

        writer.Begin(0, 1);
        writer.Sample(2, 2);
        writer.Sample(1, 9);
        writer.Sample(3, 3);
        AutomationPass? pass = writer.Stop(4, 4);

        Assert.NotNull(pass);
        Assert.Equal(new double[] { 0, 2, 3, 4 }, pass.Value.Segments[0].Select(point => point.Time));
    }

    /// <summary>Two samples at one time keep the later value: no two points share a time, so no zero-height step is written.</summary>
    [Fact]
    public void SamplesAtOneTimeKeepTheLaterValue()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Volume);

        writer.Begin(0, 1);
        writer.Sample(1, 2);
        writer.Sample(1, 3);
        AutomationPass? pass = writer.Stop(2, 4);

        Assert.NotNull(pass);
        Assert.Equal(new double[] { 0, 1, 2 }, pass.Value.Segments[0].Select(point => point.Time));
        Assert.Equal(new double[] { 1, 3, 4 }, pass.Value.Segments[0].Select(point => point.Value));
    }

    /// <summary>A pass holds at most its cap, stops recording there, and says it was truncated.</summary>
    [Fact]
    public void APassStopsAtItsCapAndSaysSo()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Volume);

        writer.Begin(0, 1);
        for (int i = 1; i <= AutomationWriter.MaxPoints + 10; i++)
        {
            writer.Sample(i * 0.001, 1);
        }
        AutomationPass? pass = writer.Stop(1e6, 1);

        Assert.NotNull(pass);
        Assert.True(pass.Value.Truncated);
        Assert.Equal(AutomationWriter.MaxPoints, pass.Value.Segments.Sum(segment => segment.Count));
    }

    /// <summary>Values the control sends outside the lane's range are kept at the range's limits.</summary>
    [Fact]
    public void ValuesAreKeptInsideTheLaneRange()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Pan);

        writer.Begin(0, 5);
        AutomationPass? pass = writer.Stop(1, -5);

        Assert.NotNull(pass);
        Assert.Equal(new double[] { 1, -1 }, pass.Value.Segments[0].Select(point => point.Value));
    }
}
