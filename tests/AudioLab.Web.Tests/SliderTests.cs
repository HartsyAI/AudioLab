using AudioLab.Web.Components;

namespace AudioLab.Web.Tests;

/// <summary>What the slider reads out: its label and value, and its unit only when it has one.</summary>
public sealed class SliderTests
{
    /// <summary>A slider with no unit is read out as its label and value, not with a unit the value does not have.</summary>
    [Fact]
    public async Task ASliderWithNoUnitIsReadWithoutOne()
    {
        RenderedMarkup markup = await RenderedMarkup.OfAsync<Slider>(new Dictionary<string, object?>
        {
            ["Label"] = "Swing",
            ["Text"] = "0.25",
            ["Unit"] = string.Empty,
        });

        Assert.Contains("Swing 0.25", markup.Values("aria-label"));
    }

    /// <summary>A volume is read out in dB, as the mixer's sliders always were.</summary>
    [Fact]
    public async Task ASliderWithoutAUnitParameterSaysDecibels()
    {
        RenderedMarkup markup = await RenderedMarkup.OfAsync<Slider>(new Dictionary<string, object?>
        {
            ["Label"] = "Volume of Track 1",
            ["Text"] = "-1.9",
        });

        Assert.Contains("Volume of Track 1 -1.9 dB", markup.Values("aria-label"));
    }
}
