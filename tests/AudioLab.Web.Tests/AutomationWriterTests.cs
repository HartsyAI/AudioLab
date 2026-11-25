using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the write pass tests: what each mode records from the transport's ticks and the control's values.</summary>
public sealed class AutomationWriterTests
{
    /// <summary>Write records every sample from the start of the pass to its stop, and the lane keeps the old curve as it was.</summary>
    [Fact]
    public void WriteRecordsEverySampleFromBeginToStop()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Volume);

        writer.Begin(0, 1);
        writer.Sample(1, 1.5);
        writer.Sample(2, 2);
        AutomationPass? pass = writer.Stop(3, 2.5);

        Assert.NotNull(pass);
        Assert.Equal(new double[] { 0, 1, 2, 3 }, pass.Value.Points.Select(point => point.Time));
        Assert.Equal(new double[] { 1, 1.5, 2, 2.5 }, pass.Value.Points.Select(point => point.Value));
        Assert.False(pass.Value.ReturnToOld);
    }

    /// <summary>Touch records only while the control is held, and hands the lane back to the old curve after the release.</summary>
    [Fact]
    public void TouchRecordsOnlyWhileHeldAndReturnsToOld()
    {
        AutomationWriter writer = new(AutomationMode.Touch, AutomationParameter.Pan);

        writer.Begin(0, 0.1);
        writer.Sample(0.5, 0.9);
        writer.Grab(1, 0.2);
        writer.Sample(1.5, 0.3);
        writer.Release(2, 0.4);
        writer.Sample(2.5, -0.9);
        AutomationPass? pass = writer.Stop(3, 0.0);

        Assert.NotNull(pass);
        Assert.Equal(new double[] { 1, 1.5, 2 }, pass.Value.Points.Select(point => point.Time));
        Assert.Equal(new double[] { 0.2, 0.3, 0.4 }, pass.Value.Points.Select(point => point.Value));
        Assert.True(pass.Value.ReturnToOld);
    }

    /// <summary>Latch records from the first touch to the stop, and keeps the value where it was left.</summary>
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
        Assert.Equal(new double[] { 1, 2, 3, 4 }, pass.Value.Points.Select(point => point.Time));
        Assert.False(pass.Value.ReturnToOld);
    }

    /// <summary>A pass that was never touched in touch or latch writes nothing, so it has nothing to commit.</summary>
    [Fact]
    public void APassThatWroteNothingHasNothingToCommit()
    {
        AutomationWriter writer = new(AutomationMode.Touch, AutomationParameter.Volume);

        writer.Begin(0, 1);
        writer.Sample(1, 2);

        Assert.Null(writer.Stop(2, 2));
    }

    /// <summary>Samples before the pass begins, and in read or off, are never recorded; a read or off writer is refused.</summary>
    [Fact]
    public void SamplesOutsideAPassAreNotRecordedAndReadAndOffAreRefused()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Volume);
        writer.Sample(0, 1);
        Assert.False(writer.Recording);
        Assert.Null(writer.Stop(1, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationWriter(AutomationMode.Read, AutomationParameter.Volume));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomationWriter(AutomationMode.Off, AutomationParameter.Pan));
    }

    /// <summary>A value the control sends outside its lane is recorded at the lane's limit, as the fader stops there.</summary>
    [Fact]
    public void ValuesAreKeptInsideTheLaneRange()
    {
        AutomationWriter writer = new(AutomationMode.Write, AutomationParameter.Pan);

        writer.Begin(0, 5);
        AutomationPass? pass = writer.Stop(1, -5);

        Assert.NotNull(pass);
        Assert.Equal(new double[] { 1, -1 }, pass.Value.Points.Select(point => point.Value));
    }
}
