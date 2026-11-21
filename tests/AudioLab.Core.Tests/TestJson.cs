using System.Text.Json.Nodes;

namespace AudioLab.Core.Tests;

/// <summary>Edits a project's JSON as a tree, to break it in exactly one way.</summary>
public static class TestJson
{
    public static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

    public static JsonNode Track(JsonObject root, int index) => root["tracks"]![index]!;

    public static JsonNode Clip(JsonObject root, int track = 0, int clip = 0) => Track(root, track)["clips"]![clip]!;
}
