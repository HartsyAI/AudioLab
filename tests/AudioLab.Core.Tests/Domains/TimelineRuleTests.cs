using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class TimelineRuleTests
{
    private static readonly TempoChangeId _first = TempoChangeId.From("tmp_1");

    private static readonly TempoChangeId _second = TempoChangeId.From("tmp_2");

    [Fact]
    public void ConstantTempoCountsTwoBeatsASecondAt120()
    {
        TempoMap map = TempoMap.From(120, []);

        Assert.Equal(6, map.BeatsAt(3));
        Assert.Equal(3, map.TimeAtBeat(6));
        Assert.Equal(120, map.BpmAt(0));
    }

    [Fact]
    public void BeatsAreCountedThroughEveryTempoChange()
    {
        TempoMap map = TempoMap.From(120, [new TempoChange(_first, 4, 60), new TempoChange(_second, 8, 240)]);

        Assert.Equal(8, map.BeatsAt(4));
        Assert.Equal(10, map.BeatsAt(6));
        Assert.Equal(12, map.BeatsAt(8));
        Assert.Equal(16, map.BeatsAt(9));
        Assert.Equal(6, map.TimeAtBeat(10));
        Assert.Equal(9, map.TimeAtBeat(16));
        Assert.Equal(120, map.BpmAt(3.999));
        Assert.Equal(60, map.BpmAt(4));
        Assert.Equal(240, map.BpmAt(100));
    }

    [Fact]
    public void ChangesAreUsedInTimeOrderAndUselessOnesAreIgnored()
    {
        TempoMap sorted = TempoMap.From(120, [new TempoChange(_first, 4, 60), new TempoChange(_second, 8, 240)]);
        TempoMap shuffled = TempoMap.From(
            120,
            [new TempoChange(_second, 8, 240), new TempoChange(_first, 4, 60), new TempoChange(TempoChangeId.From("tmp_3"), 0, 30)]);

        Assert.Equal(sorted.BeatsAt(9), shuffled.BeatsAt(9));
        Assert.Equal(120, shuffled.BpmAt(0));
    }

    [Theory]
    [InlineData(0, 120)]
    [InlineData(double.NaN, 120)]
    [InlineData(double.PositiveInfinity, 120)]
    [InlineData(-30, 1)]
    [InlineData(0.2, 1)]
    public void ANonsenseStartTempoNeverStopsTheClock(double bpm, double expected)
    {
        TempoMap map = TempoMap.From(bpm, []);

        Assert.Equal(expected, map.BpmAt(0));
        Assert.True(double.IsFinite(map.BeatsAt(10)));
    }

    [Fact]
    public void AChangeWithNoUsableTempoOrTimeIsIgnored()
    {
        TempoMap map = TempoMap.From(
            120,
            [new TempoChange(_first, 4, 0), new TempoChange(_second, double.NaN, 90), new TempoChange(TempoChangeId.From("tmp_3"), 6, -5)]);

        Assert.Equal(120, map.BpmAt(100));
    }

    [Fact]
    public void AChangeWithAnInfiniteTempoOrTimeIsIgnored()
    {
        TempoMap map = TempoMap.From(
            120,
            [new TempoChange(_first, 4, double.PositiveInfinity), new TempoChange(_second, double.PositiveInfinity, 90)]);

        Assert.Equal(120, map.BpmAt(100));
        Assert.True(double.IsFinite(map.BeatsAt(100)));
    }

    [Fact]
    public void BeatsAndTimeAreInversesThroughEveryChange()
    {
        TempoMap map = TempoMap.From(97.5, [new TempoChange(_first, 3.25, 133), new TempoChange(_second, 11, 41.5)]);

        foreach (double time in new[] { 0, 0.5, 3.25, 7, 11, 11.001, 60 })
        {
            Assert.Equal(time, map.TimeAtBeat(map.BeatsAt(time)), 9);
        }
        foreach (double beat in new[] { 0, 1.5, 5, 12.3, 20, 80 })
        {
            Assert.Equal(beat, map.BeatsAt(map.TimeAtBeat(beat)), 9);
        }
    }

    [Fact]
    public void BarsAreMeasuredInSecondsThroughTheTempoMap()
    {
        TempoMap map = TempoMap.From(120, [new TempoChange(_first, 4, 60)]);

        Assert.Equal(8, TempoMap.From(120, []).BarsFrom(0, 4, new TimeSignature(4, 4)));
        Assert.Equal(1.5, TempoMap.From(120, []).BarsFrom(2, 1, new TimeSignature(6, 8)));
        Assert.Equal(6, map.BarsFrom(2, 2, new TimeSignature(4, 4)));
    }

    [Theory]
    [InlineData(4, 4, 4, 1)]
    [InlineData(3, 4, 3, 1)]
    [InlineData(2, 2, 4, 1)]
    [InlineData(6, 8, 3, 1)]
    [InlineData(7, 8, 3.5, 0.5)]
    [InlineData(5, 8, 2.5, 0.5)]
    [InlineData(5, 16, 1.25, 0.25)]
    public void BarsAndClicksFollowTheDenominator(int numerator, int denominator, double beatsPerBar, double clickUnit)
    {
        TimeSignature signature = new(numerator, denominator);

        Assert.Equal(beatsPerBar, MeterRules.BeatsPerBar(signature));
        Assert.Equal(clickUnit, MeterRules.ClickUnit(signature));
    }

    [Fact]
    public void ContentEndIsWhereTheLastClipEnds()
    {
        ProjectDocument blank = BlankProject.Create();
        AudioClip audio = new()
        {
            Id = ClipId.From("clp_1"),
            Name = "a",
            Media = MediaId.From("med_1"),
            Start = 2,
            Duration = 10,
            Offset = 1,
            TrimEnd = 2,
            Gain = 1,
            FadeIn = 0,
            FadeOut = 0,
            Muted = false,
        };
        MidiClip midi = new()
        {
            Id = ClipId.From("clp_2"),
            Name = "m",
            Start = 5,
            Length = 8,
            NextNoteId = NoteId.First,
            Notes = [],
            ControlChanges = [],
            Muted = false,
        };
        Track track = BlankProject.Track("trk_1") with { Clips = [audio] };

        Assert.Equal(0, ProjectContent.End(blank));
        Assert.Equal(9, ProjectContent.End(blank with { Tracks = [track] }));
        Assert.Equal(13, ProjectContent.End(blank with { Tracks = [track, track with { MidiClips = [midi] }] }));
    }
}
