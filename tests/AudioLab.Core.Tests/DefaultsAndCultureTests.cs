using System.Globalization;
using System.Text.Json.Nodes;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests;

public sealed class DefaultsAndCultureTests
{
    [Fact]
    public void EveryDefaultColourIsAWellFormedColourAndThePalettesWrap()
    {
        for (int i = 0; i < 24; i++)
        {
            Assert.True(ProjectColor.IsValid(ProjectDefaults.TrackColor(i)));
            Assert.True(ProjectColor.IsValid(ProjectDefaults.SectionColor(i)));
        }
        Assert.Equal(ProjectDefaults.TrackColor(0), ProjectDefaults.TrackColor(12));
        Assert.Equal(ProjectDefaults.SectionColor(1), ProjectDefaults.SectionColor(11));
        Assert.Equal(ProjectDefaults.TrackColor(11), ProjectDefaults.TrackColor(-1));
        Assert.Equal(12, Enumerable.Range(0, 12).Select(ProjectDefaults.TrackColor).Distinct().Count());
    }

    [Theory]
    [InlineData("#fff", true)]
    [InlineData("#4a9eff", true)]
    [InlineData("#4a9eff80", true)]
    [InlineData("#fff\n", false)]
    [InlineData("#ffff", false)]
    [InlineData("fff", false)]
    [InlineData("", false)]
    public void AColourIsExactlyThreeSixOrEightHexDigitsAfterAHash(string color, bool valid)
    {
        Assert.Equal(valid, ProjectColor.IsValid(color));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void ErrorMessagesAreTheSameInEveryCulture(string culture)
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
            root["sections"]![0]!["end"] = 0.05;
            Track(root, 0)["volume"] = 8.5;
            Track(root, 0)["pan"] = -1.5;
            ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(root.ToJsonString()));
            Assert.Contains("at least 0.1 seconds", error.Message);
            Assert.Contains("tracks[0].pan: -1.5 must be from -1 to 1", error.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
