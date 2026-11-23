using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests.Domains;

public sealed class TempoMapSearchTests
{
    private static TempoChange Change(int index, double time, double bpm) => new(TempoChangeId.From("tmp-" + index), time, bpm);

    // The map as a plain walk over the changes would have it: the tempo in force at each time, and the beats summed segment by segment.
    private static (double Bpm, double Beats) Walk(double start, (double Time, double Bpm)[] changes, double time)
    {
        double bpm = start;
        double beats = 0;
        double at = 0;
        foreach ((double changeTime, double changeBpm) in changes)
        {
            if (changeTime > time)
            {
                break;
            }
            beats += (changeTime - at) * bpm / 60;
            at = changeTime;
            bpm = changeBpm;
        }
        return (bpm, beats + ((time - at) * bpm / 60));
    }

    [Fact]
    public void ASearchOverManyChangesGivesWhatAWalkOverThemGivesAtAndBetweenTheirTimes()
    {
        Random random = new(4);
        (double Time, double Bpm)[] changes = [.. Enumerable.Range(1, 2000).Select(i => (i * 0.37, 30 + (random.NextDouble() * 270)))];
        TempoMap map = TempoMap.From(100, changes.Select((c, i) => Change(i, c.Time, c.Bpm)));

        foreach (double time in changes.SelectMany(c => new[] { c.Time - 1e-9, c.Time, c.Time + 0.1 }).Append(0).Append(1e6))
        {
            (double bpm, double beats) = Walk(100, changes, Math.Max(0, time));
            Assert.Equal(bpm, map.BpmAt(Math.Max(0, time)), 9);
            Assert.Equal(beats, map.BeatsAt(Math.Max(0, time)), 6);
            Assert.Equal(Math.Max(0, time), map.TimeAtBeat(beats), 6);
        }
    }

    [Fact]
    public void TwoChangesAtOneTimeLeaveTheLaterInForceAsBefore()
    {
        TempoMap map = TempoMap.From(120, [Change(1, 2, 60), Change(2, 2, 90)]);

        Assert.Equal(90, map.BpmAt(2));
        Assert.Equal(120, map.BpmAt(1.999));
    }

    [Fact]
    public void AHundredThousandChangesAreSearchedNotWalked()
    {
        TempoMap map = TempoMap.From(100, Enumerable.Range(1, 100_000).Select(i => Change(i, i * 0.01, 60 + (i % 100))));

        double total = 0;
        for (int i = 0; i < 1_000_000; i++)
        {
            total += map.BpmAt(i * 0.001);
        }

        Assert.True(total > 0);
    }
}
