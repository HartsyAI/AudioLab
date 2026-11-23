using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>A number the page writes for the browser reads with a point, whatever language the reader's browser is set to.</summary>
public sealed class CultureTests
{
    /// <summary>The lengths written into style attributes keep their point under German, which writes 6,5.</summary>
    [Fact]
    public async Task LengthsInStyleAttributesKeepTheirPointUnderGerman()
    {
        CultureInfo german = new("de-DE");
        Assert.Equal(",", german.NumberFormat.NumberDecimalSeparator);
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = german;
        try
        {
            RenderedMarkup strip = await RenderedMarkup.OfAsync<ChannelStrip>(new Dictionary<string, object?>
            {
                ["Name"] = "Drums",
                ["ColorToken"] = "var(--track-1)",
            });
            Track track = Song.NewTrack("t1", 63.5);
            RenderedMarkup header = await RenderedMarkup.OfAsync<TrackHeader>(new Dictionary<string, object?>
            {
                ["Track"] = track,
                ["Height"] = 63.5,
                ["ColorToken"] = "var(--track-1)",
            });
            ProjectDocument song = Song.Empty();
            RenderedMarkup ruler = await RenderedMarkup.OfAsync<RulerRow>(new Dictionary<string, object?>
            {
                ["Kind"] = RulerRowKind.Bars,
                ["Document"] = song,
                ["Geometry"] = new TimelineGeometry(33.5),
                ["Bars"] = BarGrid.Lines(song, 0),
                ["Length"] = 10.25,
                ["Colors"] = new TrackColorMap([.. Enumerable.Repeat("#ee6d86", 12)]),
            });

            Assert.Contains("width: 6.5rem", strip.Values("style"));
            Assert.Contains("height: 63.5px", header.Values("style"));
            Assert.Contains("width: 343.38px", ruler.Values("style"));
            string[] styles = [.. strip.Values("style"), .. header.Values("style"), .. ruler.Values("style")];
            Assert.All(styles, style => Assert.DoesNotContain(',', style));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>The helper writes a point under any culture, and at most two decimals.</summary>
    [Fact]
    public void TheHelperWritesAPointAndAtMostTwoDecimals()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal("12.5px", Css.Px(12.5));
            Assert.Equal("6.5rem", Css.Rem(6.5));
            Assert.Equal("33.33%", Css.Percent(100.0 / 3));
            Assert.Equal("1234", Css.Number(1234));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
