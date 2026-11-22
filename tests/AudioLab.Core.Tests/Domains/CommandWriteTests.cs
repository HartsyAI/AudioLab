using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class CommandWriteTests
{
    private readonly CommandRegistry _registry = CoreCommands.CreateRegistry();

    [Fact]
    public void AWrittenCommandIsReadBackAsTheSameCommandUnderItsWireName()
    {
        MoveClip command = new() { Project = ProjectId.From("song"), Clip = ClipId.From("c1"), Start = 12.5, Track = TrackId.From("t2") };

        Assert.True(_registry.TryWrite(command, out string? name, out string? json));

        Assert.Equal("clip_move", name);
        CommandParseResult parsed = _registry.Parse(name, json);
        Assert.Equal(command, parsed.Command);
    }

    public static TheoryData<string> Names { get; } = [.. CoreCommands.CreateRegistry().Commands.Select(command => command.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void EveryRegisteredCommandIsWrittenAsTheTextItIsReadFrom(string name)
    {
        CommandDescriptor descriptor = _registry.Find(name)!;
        CommandParseResult read = _registry.Parse(name, SampleOf(descriptor.Schema).GetRawText());
        Assert.True(read.Command is not null, $"{name}: {string.Join("; ", read.Errors.Select(error => error.Message))}");

        Assert.True(_registry.TryWrite(read.Command, out string? writtenName, out string? written));

        Assert.Equal(name, writtenName);
        Assert.Equal(written, _registry.TryWrite(_registry.Parse(name, written).Command!, out _, out string? again) ? again : null);
    }

    [Fact]
    public void ACommandThatIsNotRegisteredIsNotWritten()
    {
        Assert.False(_registry.TryWrite(new UnregisteredCommand { Project = ProjectId.From("song") }, out string? name, out string? json));
        Assert.Null(name);
        Assert.Null(json);
    }

    private sealed record UnregisteredCommand : Command;

    // The smallest value a schema accepts, from its required members, so the test needs no hand-written sample for each command.
    private static JsonElement SampleOf(JsonElement schema)
    {
        return JsonSerializer.SerializeToElement(Value(schema));
    }

    private static object? Value(JsonElement schema)
    {
        if (schema.TryGetProperty("enum", out JsonElement options))
        {
            return JsonSerializer.Deserialize<object>(options[0].GetRawText());
        }
        string type = schema.TryGetProperty("type", out JsonElement kind) ? (kind.ValueKind == JsonValueKind.Array ? kind[0].GetString()! : kind.GetString()!) : "string";
        switch (type)
        {
            case "object":
                Dictionary<string, object?> members = [];
                HashSet<string> required = schema.TryGetProperty("required", out JsonElement names) ? [.. names.EnumerateArray().Select(name => name.GetString()!)] : [];
                foreach (JsonProperty property in schema.GetProperty("properties").EnumerateObject().Where(property => required.Contains(property.Name)))
                {
                    members[property.Name] = Value(property.Value);
                }
                return members;
            case "array":
                int least = schema.TryGetProperty("minItems", out JsonElement items) ? items.GetInt32() : 0;
                return Enumerable.Range(0, least).Select(_ => Value(schema.GetProperty("items"))).ToList();
            case "boolean":
                return true;
            case "integer":
            case "number":
                return schema.TryGetProperty("minimum", out JsonElement minimum) ? minimum.GetDouble() : 1;
            default:
                return schema.TryGetProperty("minLength", out JsonElement length) ? new string('a', length.GetInt32()) : "a";
        }
    }
}
