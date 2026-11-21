using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class ConverterTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1.5")]
    [InlineData("\"7\"")]
    [InlineData("null")]
    [InlineData("3000000000")]
    public void ANoteIdThatIsNotAWholeNumberFromOneIsRefused(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NoteId>(json));
    }

    [Fact]
    public void ANoteIdIsAPlainNumberOnTheWire()
    {
        Assert.Equal("5", JsonSerializer.Serialize(new NoteId(5)));
        Assert.Equal(new NoteId(5), JsonSerializer.Deserialize<NoteId>("5"));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("true")]
    [InlineData("\"../evil\"")]
    [InlineData("\"a b\"")]
    [InlineData("\"\"")]
    public void AnOptionalIdThatIsNotAnIdOrNoneIsRefused(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OptionalIdHolder>("{\"Link\":" + json + "}"));
    }

    [Fact]
    public void AnOptionalIdWrittenAsNullReadsAsNone()
    {
        Assert.Null(JsonSerializer.Deserialize<OptionalIdHolder>("{\"Link\":null}")!.Link);
    }

    [Fact]
    public void AnOptionalIdThatIsPresentRoundTrips()
    {
        OptionalIdHolder holder = new() { Link = ClipId.From("clp_1") };
        Assert.Equal(holder.Link, JsonSerializer.Deserialize<OptionalIdHolder>(JsonSerializer.Serialize(holder))!.Link);
    }
}
