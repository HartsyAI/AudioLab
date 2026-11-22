using System.Text.Json;

namespace AudioLab.Core.Tests;

public sealed class CommandSchemaTests
{
    private readonly JsonElement _schema = TestRegistry.Create().Find("shape")!.Schema;

    [Fact]
    public void TheSchemaIsAnObjectThatRefusesExtraMembersAndListsWhatIsRequired()
    {
        Assert.Equal("object", _schema.GetProperty("type").GetString());
        Assert.False(_schema.GetProperty("additionalProperties").GetBoolean());
        string[] required = [.. _schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!)];
        Assert.Equal(["kind", "level", "project", "tracks"], required.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnIdIsAStringWithTheIdPattern()
    {
        JsonElement project = _schema.GetProperty("properties").GetProperty("project");

        Assert.Equal("string", project.GetProperty("type").GetString());
        Assert.Equal("^[a-z0-9_-]+$", project.GetProperty("pattern").GetString());
        Assert.Equal(64, project.GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public void AnOptionalIdMayAlsoBeNull()
    {
        JsonElement track = _schema.GetProperty("properties").GetProperty("track");

        Assert.Equal(["string", "null"], track.GetProperty("type").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void AListIsAnArrayOfItsItemSchema()
    {
        JsonElement tracks = _schema.GetProperty("properties").GetProperty("tracks");

        Assert.Equal("array", tracks.GetProperty("type").GetString());
        Assert.Equal("^[a-z0-9_-]+$", tracks.GetProperty("items").GetProperty("pattern").GetString());
    }

    [Fact]
    public void AnEnumListsTheNamesItAccepts()
    {
        JsonElement kind = _schema.GetProperty("properties").GetProperty("kind");

        Assert.Equal(["audio", "instrument", "bus"], kind.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void ANumberIsANumber()
    {
        JsonElement level = _schema.GetProperty("properties").GetProperty("level");

        Assert.Equal("number", level.GetProperty("type").GetString());
    }
}
