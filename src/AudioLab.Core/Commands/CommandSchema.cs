using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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
        JsonNode node = WithoutNull(JsonSchemaExporter.GetJsonSchemaAsNode(info, _options));
        using JsonDocument document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        return Annotate(context, Describe(context, schema));
    }

    private static JsonNode Describe(JsonSchemaExporterContext context, JsonNode schema)
    {
        Type type = context.TypeInfo.Type;
        Type? inner = Nullable.GetUnderlyingType(type);
        if (IsId(inner ?? type))
        {
            return IdSchema(inner is not null);
        }
        if ((inner ?? type) == typeof(NoteId))
        {
            return new JsonObject
            {
                ["type"] = inner is not null ? new JsonArray("integer", "null") : "integer",
                ["minimum"] = 1,
                ["maximum"] = NoteId.Max,
            };
        }
        if (inner is not null && context.TypeInfo.Options.GetTypeInfo(inner).Converter is INamedEnumConverter optional)
        {
            return EnumSchema(optional, nullable: true);
        }
        if (context.TypeInfo.Converter is INamedEnumConverter named)
        {
            return EnumSchema(named, nullable: false);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueList<>))
        {
            JsonTypeInfo item = context.TypeInfo.Options.GetTypeInfo(type.GetGenericArguments()[0]);
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = WithoutNull(JsonSchemaExporter.GetJsonSchemaAsNode(item, _options)),
            };
        }
        if (schema is JsonObject node && node["additionalProperties"] is not null && HasExtensionData(context.TypeInfo))
        {
            node["additionalProperties"] = true;
        }
        return schema;
    }

    // A member's description and limits are attributes on the command's property; the exporter does not read them.
    private static JsonNode Annotate(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (schema is not JsonObject node || context.PropertyInfo?.AttributeProvider is not { } provider)
        {
            return schema;
        }
        foreach (object attribute in provider.GetCustomAttributes(inherit: false))
        {
            switch (attribute)
            {
                case DescriptionAttribute description:
                    node["description"] = description.Description;
                    break;
                case RangeAttribute range:
                    node["minimum"] = Convert.ToDouble(range.Minimum, CultureInfo.InvariantCulture);
                    node["maximum"] = Convert.ToDouble(range.Maximum, CultureInfo.InvariantCulture);
                    break;
                case MinLengthAttribute min:
                    node["minLength"] = min.Length;
                    break;
                case MaxLengthAttribute max:
                    node["maxLength"] = max.Length;
                    break;
                case RegularExpressionAttribute pattern:
                    node["pattern"] = pattern.Pattern;
                    break;
                case AllowedValuesAttribute allowed:
                    node["enum"] = new JsonArray([.. allowed.Values.Select(value => JsonValue.Create(Convert.ToDouble(value, CultureInfo.InvariantCulture)))]);
                    break;
            }
        }
        return schema;
    }

    // A type that keeps the members it does not know (D15) accepts them, so its schema must not forbid them.
    private static bool HasExtensionData(JsonTypeInfo info) => info.Properties.Any(property => property.IsExtensionData);

    // A list holds no null and the command itself is never null, but the exporter marks reference types as nullable.
    private static JsonNode WithoutNull(JsonNode schema)
    {
        if (schema is JsonObject node && node["type"] is JsonArray types)
        {
            string[] kept = [.. types.Select(type => type!.GetValue<string>()).Where(type => type != "null")];
            node["type"] = kept.Length == 1 ? kept[0] : new JsonArray([.. kept.Select(type => JsonValue.Create(type))]);
        }
        return schema;
    }

    private static JsonObject EnumSchema(INamedEnumConverter converter, bool nullable)
    {
        JsonArray names = new([.. converter.Names.Select(name => JsonValue.Create(name))]);
        if (nullable)
        {
            names.Add(null);
        }
        return new JsonObject { ["enum"] = names };
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
