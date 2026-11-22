using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class MediaRecordTests
{
    private static MediaRecord Valid => new()
    {
        Id = MediaId.From("med_1"),
        Owner = UserId.ForDoor("swarm", "bob"),
        ContentType = "audio/wav",
        Length = 1234,
    };

    [Fact]
    public void AValidRecordRoundTripsAndKeepsItsOwner()
    {
        MediaRecord read = MediaJson.Read(MediaJson.Write(Valid));
        Assert.Equal(Valid, read);
        Assert.Equal(UserId.ForDoor("swarm", "bob"), read.Owner);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData((1L << 40) + 1)]
    public void ALengthOutsideOneByteToOneTebibyteIsRefused(long length)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Length = length }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("wav")]
    [InlineData("Audio/WAV")]
    [InlineData("audio/wav; charset=x")]
    [InlineData("audio/")]
    [InlineData("audio/wav\n")]
    public void AContentTypeThatIsNotAMediaTypeIsRefused(string contentType)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { ContentType = contentType }));
    }

    [Fact]
    public void ARecordWithoutAWellFormedOwnerOrIdIsRefused()
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Owner = default }));
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Id = default }));
    }

    [Theory]
    [InlineData("{\"id\":\"med_1\",\"owner\":\"../x\",\"contentType\":\"audio/wav\",\"length\":5}")]
    [InlineData("{\"id\":\"med_1\",\"owner\":\"a\",\"contentType\":\"audio/wav\",\"length\":-5}")]
    [InlineData("{\"id\":\"med_1\",\"owner\":\"a\",\"contentType\":\"audio/wav\",\"length\":5,\"extra\":1}")]
    [InlineData("{\"id\":\"med_1\",\"contentType\":\"audio/wav\",\"length\":5}")]
    [InlineData("null")]
    [InlineData("nope")]
    public void ARecordFileThatBreaksTheRulesIsRefused(string json)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Read(json));
    }
}
