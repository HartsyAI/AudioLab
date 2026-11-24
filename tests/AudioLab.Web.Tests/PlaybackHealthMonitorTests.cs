using AudioLab.Core.Playback;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the monitor that says whether the engine keeps up.</summary>
public sealed class PlaybackHealthMonitorTests
{
    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static WorkletStats Stats(long events) => new(0, 2048, 2048, events, events * 128, true);

    private static HostStatus Status(double maxRenderMs, long overruns = 0) =>
        new(TransportState.Playing, 0, 48000, 1, 0, null, 100, overruns, maxRenderMs, 1, default, []);

    /// <summary>A dropout is a miss for ten seconds and the count at the first report is not.</summary>
    [Fact]
    public void ADropoutIsAMissForTenSecondsAndTheCountAtTheFirstReportIsNot()
    {
        FakeTime time = new();
        PlaybackHealthMonitor monitor = new(time);

        monitor.Observe(Stats(3));
        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
        monitor.Observe(Stats(4));
        Assert.Equal(PlaybackHealth.Dropping, monitor.Health);
        Assert.Contains("dropping out", monitor.Message);
        time.Now += TimeSpan.FromSeconds(11);
        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
        Assert.Null(monitor.Message);
    }

    /// <summary>A chunk that takes most of its time is strain and one that takes longer than it lasts is strain too.</summary>
    [Fact]
    public void AChunkThatTakesMostOfItsTimeIsStrainAndOneThatTakesLongerThanItLastsIsStrainToo()
    {
        FakeTime time = new();
        PlaybackHealthMonitor monitor = new(time);

        monitor.Observe(Status(2));
        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
        monitor.Observe(Status(9));
        Assert.Equal(PlaybackHealth.Strained, monitor.Health);
        time.Now += TimeSpan.FromSeconds(11);
        monitor.Observe(Status(1, overruns: 1));
        Assert.Equal(PlaybackHealth.Strained, monitor.Health);
    }

    /// <summary>A miss is forgotten when the engine starts again.</summary>
    [Fact]
    public void AMissIsForgottenWhenTheEngineStartsAgain()
    {
        PlaybackHealthMonitor monitor = new(new FakeTime());
        monitor.Observe(Stats(0));
        monitor.Observe(Stats(2));

        monitor.Reset();

        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
    }

    // Reports of the worklet every 40 ms for the seconds given, with the context running at the speed given against the wall clock.
    private static void Run(PlaybackHealthMonitor monitor, FakeTime time, double seconds, double speed, ref double frames)
    {
        for (int i = 0; i < seconds * 25; i++)
        {
            time.Now += TimeSpan.FromMilliseconds(40);
            frames += 48000 * 0.04 * speed;
            monitor.Observe(new WorkletStats(0, 2048, 2048, 0, 0, true, frames));
        }
    }

    /// <summary>A context that runs at 64% of real time is reported after ten seconds, though no dropout is counted.</summary>
    [Fact]
    public void AContextRunningSlowIsReportedAfterTenSecondsThoughNoDropoutIsCounted()
    {
        FakeTime time = new();
        PlaybackHealthMonitor monitor = new(time);
        monitor.Observe(Status(2));
        double frames = 1000;

        Run(monitor, time, 9, 0.64, ref frames);
        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
        Run(monitor, time, 3, 0.64, ref frames);

        Assert.Equal(PlaybackHealth.Drifting, monitor.Health);
        Assert.InRange(monitor.ClockRatio!.Value, 0.63, 0.65);
        Assert.Contains("64%", monitor.Message);
        Run(monitor, time, 21, 1, ref frames);
        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
    }

    /// <summary>A context within 2% of the clock is not reported, and neither is a page that was suspended for a while.</summary>
    [Fact]
    public void AContextWithinTwoPercentIsNotReportedAndNeitherIsAPageThatWasSuspended()
    {
        FakeTime time = new();
        PlaybackHealthMonitor monitor = new(time);
        monitor.Observe(Status(2));
        double frames = 1000;

        Run(monitor, time, 12, 0.985, ref frames);
        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
        time.Now += TimeSpan.FromSeconds(30);
        Run(monitor, time, 12, 1, ref frames);

        Assert.Equal(PlaybackHealth.Steady, monitor.Health);
        Assert.InRange(monitor.ClockRatio!.Value, 0.99, 1.01);
    }
}
