using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The fader's scale: a gain above its top mark sits at the top and reads its true value.</summary>
public sealed class FaderScaleTests
{
    /// <summary>A gain above +6 dB sits at the top of the fader and its readout is the true gain, up to the project's ceiling.</summary>
    [Fact]
    public void AGainAboveTheTopMarkSitsAtTheTopAndReadsItsTrueValue()
    {
        Assert.Equal(1, FaderScale.Position(4));
        Assert.Equal("+12.0 dB", FaderScale.Readout(4));
        Assert.Equal(1, FaderScale.Position(ProjectLimits.MaxGain));
        Assert.Equal("+24.1 dB", FaderScale.Readout(ProjectLimits.MaxGain));
    }
}
