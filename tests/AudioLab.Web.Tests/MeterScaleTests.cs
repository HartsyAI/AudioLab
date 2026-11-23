using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the meter scale.</summary>
public sealed class MeterScaleTests
{
    /// <summary>A level is drawn on a straight decibel scale so the colour steps fall at minus twelve and minus three.</summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1e-6, 0.0)]
    [InlineData(0.2511886, 0.6829)]
    [InlineData(0.7079458, 0.9024)]
    [InlineData(1.0, 0.9756)]
    [InlineData(4.0, 1.0)]
    public void ALevelIsDrawnOnAStraightDecibelScaleSoTheColourStepsFallAtMinusTwelveAndMinusThree(double level, double height)
    {
        Assert.Equal(height, MeterScale.Height(level), 3);
    }

    /// <summary>A sample that is not a number draws as silence.</summary>
    [Fact]
    public void ASampleThatIsNotANumberDrawsAsSilence()
    {
        Assert.Equal(0, MeterScale.Height(double.NaN));
        Assert.Equal(0, MeterScale.Height(-0.5));
    }
}
