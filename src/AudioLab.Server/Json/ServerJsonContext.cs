using System.Text.Json.Serialization;
using AudioLab.Server.Services;

namespace AudioLab.Server.Json;

/// <summary>Source-generated JSON for the server's own records. Project documents go through <c>ProjectJson</c> in Core instead.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HealthReport))]
[JsonSerializable(typeof(SessionInfo))]
public sealed partial class ServerJsonContext : JsonSerializerContext
{
}
