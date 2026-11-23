using AudioLab.Web.Waveforms;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of which level of a peak file a zoom is drawn from.</summary>
public sealed class WaveformLevelTests
{
    private static readonly int[] _levels = [64, 256, 1024, 4096, 16384];

    /// <summary>The coarsest level with a peak for every pixel is chosen, and the finest when the view is closer than that.</summary>
    [Theory]
    [InlineData(44100, 34, 2)]
    [InlineData(48000, 34, 2)]
    [InlineData(44100, 1, 4)]
    [InlineData(44100, 2.7, 3)]
    [InlineData(44100, 10, 3)]
    [InlineData(44100, 100, 1)]
    [InlineData(44100, 500, 0)]
    [InlineData(44100, 689.0625, 0)]
    [InlineData(44100, 172.265625, 1)]
    [InlineData(8000, 34, 0)]
    [InlineData(384000, 34, 3)]
    [InlineData(44100, 100000, 0)]
    public void TheCoarsestLevelWithAPeakForEveryPixelIsChosen(int sampleRate, double pixelsPerSecond, int expected)
    {
        Assert.Equal(expected, WaveformLevel.For(_levels, sampleRate, pixelsPerSecond));
    }

    /// <summary>A pixel never covers fewer frames than the level peak it is drawn from, except in the finest level.</summary>
    [Theory]
    [InlineData(44100, 20)]
    [InlineData(44100, 34)]
    [InlineData(48000, 68)]
    [InlineData(96000, 5)]
    public void APixelNeverCoversFewerFramesThanThePeakItIsDrawnFrom(int sampleRate, double pixelsPerSecond)
    {
        int level = WaveformLevel.For(_levels, sampleRate, pixelsPerSecond);

        double framesPerPixel = sampleRate / pixelsPerSecond;
        Assert.True(level == 0 || _levels[level] <= framesPerPixel);
        Assert.True(level == _levels.Length - 1 || _levels[level + 1] > framesPerPixel);
    }

    /// <summary>A zoom that makes no sense is refused.</summary>
    [Fact]
    public void AZoomThatMakesNoSenseIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WaveformLevel.For(_levels, 44100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaveformLevel.For(_levels, 0, 34));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaveformLevel.For([], 44100, 34));
    }
}
