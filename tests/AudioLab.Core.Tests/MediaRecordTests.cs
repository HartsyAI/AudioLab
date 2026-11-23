using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class MediaRecordTests
{
    private static readonly UserId _bob = UserId.ForDoor("swarm", "bob");

    private static MediaRecord Valid => TestMedia.Record(owner: _bob);

    [Fact]
    public void AValidRecordRoundTripsAndKeepsItsOwner()
    {
        MediaRecord read = MediaJson.Read(MediaJson.Write(Valid));
        Assert.Equal(Valid, read);
        Assert.Equal(_bob, read.Owner);
    }

    [Fact]
    public void TheRecordIsWrittenWithNamedEnumsAndAUtcTime()
    {
        string json = MediaJson.Write(Valid);
        Assert.Contains("\"kind\":\"audio\"", json, StringComparison.Ordinal);
        Assert.Contains("\"format\":\"wav\"", json, StringComparison.Ordinal);
        Assert.Contains("\"encoding\":\"pcm16\"", json, StringComparison.Ordinal);
        Assert.Contains("\"created\":\"2026-11-22T10:00:00+00:00\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData((1L << 40) + 1)]
    public void ABytesCountOutsideOneByteToOneTebibyteIsRefused(long bytes)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Bytes = bytes }));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(ProjectLimits.MaxSeconds + 1)]
    public void ADurationThatIsNotAPositiveFiniteLengthIsRefused(double duration)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Duration = duration }));
    }

    [Theory]
    [InlineData(7999, 2)]
    [InlineData(384_001, 2)]
    [InlineData(48000, 0)]
    [InlineData(48000, 9)]
    public void ARateOrChannelCountOutsideTheLimitsIsRefused(int sampleRate, int channels)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { SampleRate = sampleRate, Channels = channels }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\nb")]
    [InlineData("a\u202Eb")]
    public void ANameThatIsNotACleanLabelIsRefused(string name)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Name = name }));
        Assert.NotEmpty(MediaValidator.ValidateName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("abc")]
    [InlineData("zzzz0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd")]
    public void AHashThatIsNotLowerCaseSha256HexIsRefused(string hash)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Sha256 = hash }));
    }

    [Fact]
    public void ATimeThatIsNotUtcAndAnUnknownEnumAreRefused()
    {
        DateTimeOffset local = new(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Created = local }));
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Encoding = (SampleEncoding)99 }));
    }

    [Fact]
    public void ARecordWithoutAWellFormedOwnerOrIdIsRefused()
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Owner = default }));
        Assert.Throws<ProjectFormatException>(() => MediaJson.Write(Valid with { Id = default }));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("nope")]
    [InlineData("{}")]
    [InlineData("{\"id\":\"med_1\",\"owner\":\"a\",\"contentType\":\"audio/wav\",\"length\":5}")]
    public void ARecordFileThatIsOldOrBreaksTheRulesIsRefused(string json)
    {
        Assert.Throws<ProjectFormatException>(() => MediaJson.Read(json));
    }

    [Fact]
    public void AnUnknownMemberOrAnOwnerThatIsAPathIsRefused()
    {
        string json = MediaJson.Write(Valid);
        Assert.Throws<ProjectFormatException>(() => MediaJson.Read(json.Replace("{", "{\"extra\":1,", StringComparison.Ordinal)));
        Assert.Throws<ProjectFormatException>(() => MediaJson.Read(json.Replace(_bob.Value, "../x", StringComparison.Ordinal)));
        Assert.Throws<ProjectFormatException>(() => MediaJson.Read(json.Replace("\"pcm16\"", "\"PCM16\"", StringComparison.Ordinal)));
    }
}
