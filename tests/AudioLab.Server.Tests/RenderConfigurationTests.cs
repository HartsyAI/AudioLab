using AudioLab.Server.Routes;

namespace AudioLab.Server.Tests;

public sealed class RenderConfigurationTests
{
    [Theory]
    [InlineData("MaxDecodedBytesPerFile", "1000")]
    [InlineData("MaxDecodedBytesPerRender", "1000")]
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
    [InlineData("MaxFileBytes", "100")]
    [InlineData("MaxFileBytes", "67108865")]
    [InlineData("MaxReadTime", "00:00:00")]
    [InlineData("MaxReadTime", "02:00:00")]
    [InlineData("MaxConcurrentPerUser", "0")]
    [InlineData("MaxConcurrent", "0")]
    public void AMidiLimitThatCannotWorkDoesNotStart(string option, string value)
    {
        Dictionary<string, string?> settings = new() { ["urls"] = "http://127.0.0.1:0", [$"AudioLab:Midi:{option}"] = value };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));

        Assert.Contains($"AudioLab:Midi:{option}", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CON", true)]
    [InlineData("con", true)]
    [InlineData("Nul.txt", true)]
    [InlineData("COM1 .x", true)]
    [InlineData("LPT9", true)]
    [InlineData("COM0", true)]
    [InlineData("lpt0", true)]
    [InlineData("COM\u00B2", true)]
    [InlineData("LPT\u00B3.txt", true)]
    [InlineData("CONIN$", true)]
    [InlineData("conout$", true)]
    [InlineData("COM10", false)]
    [InlineData("COMX", false)]
    [InlineData("Console", false)]
    [InlineData("My CON", false)]
    public void ADeviceNameIsRecognisedWithOrWithoutAnExtension(string name, bool expected)
    {
        Assert.Equal(expected, DeviceNames.IsDeviceName(name));
    }
}
