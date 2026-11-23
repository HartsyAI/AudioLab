using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AudioLab.Core.Commands;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>Reads a tool's arguments as strictly as the tool's schema says: a missing, unknown or mistyped member is a refusal.</summary>
public static class McpArguments
{
    /// <summary>The arguments as <typeparamref name="T"/>, or null with the refusal to return.</summary>
    public static T? Read<T>(IDictionary<string, JsonElement>? arguments, JsonTypeInfo<T> info, out CallToolResult? refusal)
        where T : class
    {
        try
        {
            JsonElement element = JsonSerializer.SerializeToElement(
                arguments ?? new Dictionary<string, JsonElement>(),
                McpJsonContext.Default.IDictionaryStringJsonElement);
            T? value = element.Deserialize(info);
            if (value is not null)
            {
                refusal = null;
                return value;
            }
            refusal = McpResults.Refused(CommandErrorCode.Malformed, "The arguments are not an object.");
        }
        catch (JsonException exception)
        {
            refusal = McpResults.Refused(CommandErrorCode.Malformed, $"The arguments are not valid: {exception.Message}");
        }
        return null;
    }
}
