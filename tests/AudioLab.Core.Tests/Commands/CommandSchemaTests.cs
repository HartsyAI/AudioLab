using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;

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
        Assert.Equal(["flags", "kind", "labels", "level", "project", "tracks"], required.Order(StringComparer.Ordinal));
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
    public void AnOptionalEnumMayAlsoBeNull()
    {
        JsonElement mode = _schema.GetProperty("properties").GetProperty("mode");

        Assert.Equal(
            ["audio", "instrument", "bus", null],
            mode.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void ListsOfPlainValuesKeepTheirItemTypeAndAllowNoNull()
    {
        JsonElement properties = _schema.GetProperty("properties");

        Assert.Equal("boolean", properties.GetProperty("flags").GetProperty("items").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("labels").GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public void ANumberIsANumber()
    {
        JsonElement level = _schema.GetProperty("properties").GetProperty("level");

        Assert.Equal("number", level.GetProperty("type").GetString());
    }

    [Fact]
    public void NoMemberOfTheProjectModelHasAnEmptySchema()
    {
        JsonTypeInfo[] roots = [ProjectJsonContext.Default.ProjectDocument, ProjectJsonContext.Default.MediaRecord];
        List<string> empty = [];

        foreach (JsonTypeInfo root in roots)
        {
            Walk(CommandSchema.For(root), root.Type.Name, empty);
        }

        Assert.Empty(empty);
    }

    [Fact]
    public void ANoteIdIsAWholeNumberFromOne()
    {
        JsonElement note = CommandSchema.For(ProjectJsonContext.Default.MidiClip)
            .GetProperty("properties").GetProperty("nextNoteId");

        Assert.Equal("integer", note.GetProperty("type").GetString());
        Assert.Equal(1, note.GetProperty("minimum").GetInt32());
    }

    // A node that says nothing about its value, which is what an unhandled custom converter yields.
    private static void Walk(JsonElement node, string path, List<string> empty)
    {
        string[] describing = ["type", "enum", "anyOf", "oneOf", "allOf", "$ref", "const", "properties"];
        if (node.ValueKind != JsonValueKind.Object || !describing.Any(key => node.TryGetProperty(key, out _)))
        {
            empty.Add(path);
            return;
        }
        if (node.TryGetProperty("properties", out JsonElement properties))
        {
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                Walk(property.Value, $"{path}.{property.Name}", empty);
            }
        }
        if (node.TryGetProperty("items", out JsonElement items))
        {
            Walk(items, $"{path}[]", empty);
        }
        if (node.TryGetProperty("anyOf", out JsonElement alternatives))
        {
            foreach (JsonElement alternative in alternatives.EnumerateArray())
            {
                Walk(alternative, path, empty);
            }
        }
    }
}
