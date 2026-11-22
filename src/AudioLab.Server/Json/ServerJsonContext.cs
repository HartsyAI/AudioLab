using System.Text.Json.Serialization;
using AudioLab.Server.Services;

namespace AudioLab.Server.Json;

/// <summary>Source-generated JSON for the server's own records.</summary>
/// <remarks>Project documents go through <c>ProjectJson</c> in Core and the route records through <c>ApiJsonContext</c>.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HealthReport))]
[JsonSerializable(typeof(string[]))]
public sealed partial class ServerJsonContext : JsonSerializerContext
{
}
