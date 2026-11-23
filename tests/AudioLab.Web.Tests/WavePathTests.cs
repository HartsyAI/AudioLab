using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Waveforms;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the path a clip waveform is drawn with.</summary>
public sealed class WavePathTests
{
    private const int Rate = 48000;

    private static PeakLevel Level(float[] interleaved, int channels, int index)
    {
        PeakBuilder builder = new(channels, Rate);
        builder.AddFrames(interleaved);
        return builder.Build().Levels[index];
    }

    private static string[] Points(string path) => [.. path.Split(' ').Where(part => part.StartsWith('L')).Select(part => part[1..])];

    // The y of every point of the path, top edge first, then the bottom edge back.
    private static double[] Ys(string path) => [.. Points(path).Select(point => double.Parse(point.Split(',')[1], CultureInfo.InvariantCulture))];

    private static double[] Xs(string path) => [.. Points(path).Select(point => double.Parse(point.Split(',')[0], CultureInfo.InvariantCulture))];

    private static float[] Loud(int frames, int from, int to, float value = 0.5f)
    {
        float[] samples = new float[frames];
        for (int frame = from; frame < to; frame++)
        {
            samples[frame] = frame % 2 == 0 ? value : -value;
        }
        return samples;
    }

    /// <summary>A path is closed, has a top and a bottom point for each column, and runs across the box left to right then back.</summary>
    [Fact]
    public void APathHasATopAndABottomPointForEachColumnAndIsClosed()
    {
        PeakLevel level = Level(Loud(48000, 0, 48000), 1, 1);

        string path = WavePath.Build(level, 1, Rate, 0, 1, 10);

        Assert.StartsWith("M0,50 L0,", path, StringComparison.Ordinal);
        Assert.EndsWith(" Z", path, StringComparison.Ordinal);
        double[] x = Xs(path);
        Assert.Equal(20, x.Length);
        Assert.Equal(x.Take(10).Order(), x.Take(10));
        Assert.Equal(1000, x[9]);
        Assert.Equal(x.Take(10).Reverse(), x.Skip(10));
    }

    /// <summary>The window selects the part of the file that is drawn: loud only in the second half shows only in the second half.</summary>
    [Fact]
    public void TheWindowSelectsThePartOfTheFileThatIsDrawn()
    {
        PeakLevel level = Level(Loud(96000, 48000, 96000), 1, 1);

        double[] whole = [.. Ys(WavePath.Build(level, 1, Rate, 0, 2, 8)).Take(8)];
        double[] first = [.. Ys(WavePath.Build(level, 1, Rate, 0, 1, 8)).Take(8)];
        double[] second = [.. Ys(WavePath.Build(level, 1, Rate, 1, 1, 8)).Take(8)];

        Assert.All(whole.Take(3), y => Assert.Equal(49.5, y));
        Assert.All(whole.Skip(5), y => Assert.True(y < 40));
        Assert.All(first.Take(7), y => Assert.Equal(49.5, y));
        Assert.All(second, y => Assert.True(y < 40));
    }

    /// <summary>The top follows the highest sample and the bottom the lowest, both scaled to the box.</summary>
    [Fact]
    public void TheTopFollowsTheHighestSampleAndTheBottomTheLowest()
    {
        float[] samples = new float[4096];
        samples[100] = 0.5f;
        samples[200] = -0.25f;
        PeakLevel level = Level(samples, 1, 2);

        double[] y = Ys(WavePath.Build(level, 1, Rate, 0, 4096.0 / Rate, 2));

        Assert.Equal(50 - (0.5 * 46), y[0], 1);
        Assert.Equal(50 + (0.25 * 46), y[3], 1);
    }

    /// <summary>All channels count: a column is as high as the loudest channel and as low as the lowest.</summary>
    [Fact]
    public void AllChannelsCount()
    {
        float[] samples = new float[2 * 2048];
        samples[0] = 0.3f;
        samples[1] = -0.8f;
        PeakLevel level = Level(samples, 2, 2);

        double[] y = Ys(WavePath.Build(level, 2, Rate, 0, 2048.0 / Rate, 2));

        Assert.Equal(50 - (0.3 * 46), y[0], 1);
        Assert.Equal(50 + (0.8 * 46), y[3], 1);
    }

    /// <summary>Silence keeps the thinnest line instead of vanishing.</summary>
    [Fact]
    public void SilenceKeepsTheThinnestLine()
    {
        PeakLevel level = Level(new float[48000], 1, 1);

        double[] y = Ys(WavePath.Build(level, 1, Rate, 0, 1, 4));

        Assert.All(y.Take(4), value => Assert.Equal(49.5, value));
        Assert.All(y.Skip(4), value => Assert.Equal(50.5, value));
    }

    /// <summary>A window that reaches the end of the file, or a view closer than the finest level, still draws every column.</summary>
    [Theory]
    [InlineData(0.0, 1.0, 400)]
    [InlineData(0.9, 0.1, 50)]
    [InlineData(0.5, 0.001, 30)]
    public void EveryColumnIsDrawnAtTheEdgesAndCloserThanTheFinestLevel(double offset, double visible, int columns)
    {
        PeakLevel level = Level(Loud(48000, 0, 48000), 1, 0);

        string path = WavePath.Build(level, 1, Rate, offset, visible, columns);

        Assert.Equal(columns * 2, Ys(path).Length);
        Assert.All(Ys(path), y => Assert.InRange(y, 0, 100));
    }

    /// <summary>A clip is drawn with one column for each pixel of its width, within limits.</summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(340, 340)]
    [InlineData(340.4, 340)]
    [InlineData(6120, 2500)]
    public void AClipIsDrawnWithOneColumnForEachPixelOfItsWidth(double width, int columns)
    {
        Assert.Equal(columns, WavePath.Columns(width));
    }
}
