using AudioLab.Server.Render;

namespace AudioLab.Server.Tests;

public sealed class RenderConfigurationTests
{
    [Theory]
    [InlineData("MaxDecodedBytesPerFile", "1000")]
    [InlineData("MediaCacheBytes", "0")]
    [InlineData("MaxConcurrentExportsPerUser", "0")]
    [InlineData("MaxConcurrentExports", "0")]
    [InlineData("MaxSeconds", "0")]
    [InlineData("MaxSeconds", "2000000")]
    [InlineData("MaxBytes", "1000")]
    [InlineData("MaxBytes", "4294967296")]
    [InlineData("MaxTime", "00:00:00")]
    [InlineData("MaxTime", "2.00:00:00")]
    [InlineData("MaxTailSeconds", "-1")]
    [InlineData("MaxTailSeconds", "601")]
    public void ARenderLimitThatCannotWorkDoesNotStart(string option, string value)
    {
        Dictionary<string, string?> settings = new() { ["urls"] = "http://127.0.0.1:0", [$"AudioLab:Render:{option}"] = value };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));

        Assert.Contains($"AudioLab:Render:{option}", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CON", true)]
    [InlineData("con", true)]
    [InlineData("Nul.txt", true)]
    [InlineData("COM1 .x", true)]
    [InlineData("LPT9", true)]
    [InlineData("COM0", false)]
    [InlineData("Console", false)]
    [InlineData("My CON", false)]
    public void ADeviceNameIsRecognisedWithOrWithoutAnExtension(string name, bool expected)
    {
        Assert.Equal(expected, DeviceNames.IsDeviceName(name));
    }
}
