using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Builds the JSON Schema of a command from its source-generated type info.</summary>
/// <remarks>The exporter treats a custom converter as "any value", so ids, enums and lists are spelled out here.</remarks>
internal static class CommandSchema
{
    private static readonly JsonSchemaExporterOptions _options = new() { TransformSchemaNode = Transform };

    /// <summary>The schema of <paramref name="info"/> as a detached JSON element.</summary>
    public static JsonElement For(JsonTypeInfo info)
    {
        JsonNode node = JsonSchemaExporter.GetJsonSchemaAsNode(info, _options);
        using JsonDocument document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        Type type = context.TypeInfo.Type;
        Type? inner = Nullable.GetUnderlyingType(type);
        if (IsId(inner ?? type))
        {
            return IdSchema(inner is not null);
        }
        if (context.TypeInfo.Converter is INamedEnumConverter named)
        {
            return new JsonObject { ["enum"] = new JsonArray([.. named.Names.Select(name => JsonValue.Create(name))]) };
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueList<>))
        {
            JsonTypeInfo item = context.TypeInfo.Options.GetTypeInfo(type.GetGenericArguments()[0]);
            return new JsonObject { ["type"] = "array", ["items"] = JsonSchemaExporter.GetJsonSchemaAsNode(item, _options) };
        }
        if (context.Path.IsEmpty && schema is JsonObject root)
        {
            root["type"] = "object";
        }
        return schema;
    }

    private static bool IsId(Type type)
    {
        return type.IsValueType
            && type.GetInterfaces().Any(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IId<>));
    }

    private static JsonObject IdSchema(bool nullable)
    {
        return new JsonObject
        {
            ["type"] = nullable ? new JsonArray("string", "null") : "string",
            ["minLength"] = 1,
            ["maxLength"] = IdRules.MaxLength,
            ["pattern"] = "^[a-z0-9_-]+$",
            ["description"] = IdRules.Description,
        };
    }
}
