using System.Text.Json;

namespace AudioLab.Core.Tests;

/// <summary>Finds the members of a JSON Schema that say nothing about their value.</summary>
internal static class SchemaWalk
{
    // A node that says nothing about its value, which is what an unhandled custom converter yields.
    public static void Empty(JsonElement node, string path, List<string> empty)
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
                Empty(property.Value, $"{path}.{property.Name}", empty);
            }
        }
        if (node.TryGetProperty("items", out JsonElement items))
        {
            Empty(items, $"{path}[]", empty);
        }
        if (node.TryGetProperty("anyOf", out JsonElement alternatives))
        {
            foreach (JsonElement alternative in alternatives.EnumerateArray())
            {
                Empty(alternative, path, empty);
            }
        }
    }
}
