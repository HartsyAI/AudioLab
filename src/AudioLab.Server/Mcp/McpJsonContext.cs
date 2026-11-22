using System.Text.Json;
using System.Text.Json.Serialization;
using AudioLab.Server.Store;

namespace AudioLab.Server.Mcp;

/// <summary>Source-generated JSON for the MCP tools' inputs and results.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(NoArguments))]
[JsonSerializable(typeof(ProjectArguments))]
[JsonSerializable(typeof(ProjectDocumentResult))]
[JsonSerializable(typeof(ProjectListing))]
[JsonSerializable(typeof(HistoryState))]
[JsonSerializable(typeof(IDictionary<string, JsonElement>))]
public sealed partial class McpJsonContext : JsonSerializerContext
{
}
