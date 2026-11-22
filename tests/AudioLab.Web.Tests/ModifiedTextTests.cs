using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the modified text tests.</summary>
public sealed class ModifiedTextTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Recent changes are said in minutes and hours.</summary>
    [Theory]
    [InlineData(30, "just now")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData(3 * 3600, "3 h ago")]
    public void RecentChangesAreSaidInMinutesAndHours(int secondsAgo, string expected)
    {
        Assert.Equal(expected, ModifiedText.Of(_now.AddSeconds(-secondsAgo), _now, TimeZoneInfo.Utc));
    }

    /// <summary>An earlier day is yesterday then the date.</summary>
    [Fact]
    public void AnEarlierDayIsYesterdayThenTheDate()
    {
        Assert.Equal("yesterday", ModifiedText.Of(new DateTimeOffset(2026, 11, 21, 23, 0, 0, TimeSpan.Zero), _now, TimeZoneInfo.Utc));
        Assert.Equal("19 Nov 2026", ModifiedText.Of(new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero), _now, TimeZoneInfo.Utc));
    }

    /// <summary>The day is the viewers own not utc.</summary>
    [Fact]
    public void TheDayIsTheViewersOwnNotUtc()
    {
        TimeZoneInfo plusNine = TimeZoneInfo.CreateCustomTimeZone("plus9", TimeSpan.FromHours(9), "plus9", "plus9");
        DateTimeOffset now = new(2026, 11, 22, 1, 0, 0, TimeSpan.Zero);

        Assert.Equal("yesterday", ModifiedText.Of(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero), now, plusNine));
    }
}
