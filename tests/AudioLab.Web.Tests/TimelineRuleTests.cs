using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

public sealed class TimelineRuleTests
{
    [Fact]
    public void BarsStartWhereTheTempoMapPutsThemAndEveryFourthIsStrong()
    {
        IReadOnlyList<BarLine> lines = BarGrid.Lines(Song.Empty(), 0);

        Assert.Equal(BarGrid.MinimumBars, lines.Count);
        Assert.Equal(new BarLine(1, 0, true), lines[0]);
        Assert.Equal(new BarLine(2, 2, false), lines[1]);
        Assert.Equal(new BarLine(5, 8, true), lines[4]);
    }

    [Fact]
    public void ATempoChangeMovesTheBarsAfterItAndTheyStayEvenlyTimedWithinEachTempo()
    {
        ProjectDocument slowing = Song.Empty(120, [new TempoChange(TempoChangeId.From("t1"), 4, 60)]);

        IReadOnlyList<BarLine> lines = BarGrid.Lines(slowing, 0);

        Assert.Equal(4, lines[2].Time);
        Assert.Equal(8, lines[3].Time);
        Assert.Equal(12, lines[4].Time);
    }

    [Fact]
    public void BarLinesFollowALongArrangementButNeverPastTheCap()
    {
        Assert.Equal(50, BarGrid.Lines(Song.Empty(), 100).Count);
        Assert.Equal(BarGrid.MaximumBars, BarGrid.Lines(Song.Empty(), 1_000_000).Count);
    }

    [Fact]
    public void TheCounterNamesTheBarBeatAndSixteenthAtATime()
    {
        Assert.Equal("1.1.1", BarGrid.Position(Song.Empty(), 0));
        Assert.Equal("10.2.1", BarGrid.Position(Song.Empty(), 18.5));
        Assert.Equal("1.1.1", BarGrid.Position(Song.Empty(), -3));
    }

    [Fact]
    public void TheLanesCoverAtLeastTwentyFourBarsAndReachFourBarsPastTheLastClip()
    {
        Assert.Equal(48, SessionExtent.LengthSeconds(Song.Empty()));
        ProjectDocument long_ = Song.Empty().With(Song.NewTrack("a", 64, Song.Audio("c1", 100, 11)));

        Assert.Equal(120, SessionExtent.LengthSeconds(long_));
    }

    [Fact]
    public void RowsStackAtTheHeightsTheDocumentKeepsAndNoRowIsTooSmallToUse()
    {
        ProjectDocument document = Song.Empty().With(Song.NewTrack("a", 64), Song.NewTrack("b", 100), Song.NewTrack("c", 2));

        TrackLayout layout = TrackLayout.Of(document);

        Assert.Equal([0, 64, 164], layout.Tops);
        Assert.Equal([64, 100, TrackLayout.MinimumHeight], layout.Heights);
        Assert.Equal(180, layout.Total);
    }

    [Fact]
    public void TheScaleIsLinearInSeconds()
    {
        TimelineGeometry geometry = TimelineGeometry.Default;

        Assert.Equal(68, geometry.Width(2));
        Assert.Equal(34, geometry.X(1));
        Assert.Equal(2, geometry.Seconds(68));
    }

    [Theory]
    [InlineData(0.0, "0:00:00.000")]
    [InlineData(18.5, "0:00:18.500")]
    [InlineData(3725.0004, "1:02:05.000")]
    [InlineData(-4.0, "0:00:00.000")]
    public void TheCounterWritesAFixedWidthTime(double seconds, string expected)
    {
        Assert.Equal(expected, TimeText.Counter(seconds));
    }

    [Fact]
    public void TheRulerWritesMinutesAndSecondsAndHoursOnlyWhenThereAreSome()
    {
        Assert.Equal("1:05", TimeText.Ruler(65.9));
        Assert.Equal("1:00:09", TimeText.Ruler(3609));
    }
}
