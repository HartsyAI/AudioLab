using AudioLab.Core.Model;
using AudioLab.Web.Media;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of how the library writes a file's length, format and size.</summary>
public sealed class MediaTextTests
{
    private static MediaSummary File(SampleEncoding encoding, int rate, int channels) =>
        new("m1", "a.wav", MediaKind.Audio, MediaFormat.Wav, encoding, 1, rate, channels, 1, new string('0', 64), DateTimeOffset.UnixEpoch);

    /// <summary>A length is minutes, seconds and tenths, with hours only when there are some.</summary>
    [Theory]
    [InlineData(0, "0:00.0")]
    [InlineData(1.5, "0:01.5")]
    [InlineData(12.34, "0:12.3")]
    [InlineData(59.96, "1:00.0")]
    [InlineData(187.4, "3:07.4")]
    [InlineData(3723.04, "1:02:03.0")]
    [InlineData(-5, "0:00.0")]
    public void ALengthIsMinutesSecondsAndTenths(double seconds, string expected)
    {
        Assert.Equal(expected, MediaText.Length(seconds));
    }

    /// <summary>A size uses the binary unit the server's limits are written in.</summary>
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KiB")]
    [InlineData(264_644, "258 KiB")]
    [InlineData(12_998_144, "12.4 MiB")]
    [InlineData(536_870_912, "512 MiB")]
    [InlineData(21_474_836_480, "20 GiB")]
    public void ASizeUsesTheBinaryUnit(long bytes, string expected)
    {
        Assert.Equal(expected, MediaText.Size(bytes));
    }

    /// <summary>The format says the sample format, the rate and the channels.</summary>
    [Fact]
    public void TheFormatSaysTheSampleFormatTheRateAndTheChannels()
    {
        Assert.Equal("16-bit 44.1 kHz stereo", MediaText.Format(File(SampleEncoding.Pcm16, 44100, 2)));
        Assert.Equal("24-bit 48 kHz mono", MediaText.Format(File(SampleEncoding.Pcm24, 48000, 1)));
        Assert.Equal("32-bit float 96 kHz 6 channels", MediaText.Format(File(SampleEncoding.Float32, 96000, 6)));
        Assert.Equal("32-bit 22.05 kHz stereo", MediaText.Format(File(SampleEncoding.Pcm32, 22050, 2)));
    }

    /// <summary>The wait for the server is in seconds, then minutes.</summary>
    [Fact]
    public void TheWaitIsInSecondsThenMinutes()
    {
        Assert.Equal("0 s", MediaText.Elapsed(TimeSpan.FromMilliseconds(900)));
        Assert.Equal("59 s", MediaText.Elapsed(TimeSpan.FromSeconds(59.9)));
        Assert.Equal("1 min 5 s", MediaText.Elapsed(TimeSpan.FromSeconds(65)));
    }
}
