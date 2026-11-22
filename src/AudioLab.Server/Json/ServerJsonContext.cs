using System.Text.Json.Serialization;
using AudioLab.Server.Services;
using AudioLab.Server.Store;

namespace AudioLab.Server.Json;

/// <summary>Source-generated JSON for the server's own records. Project documents go through <c>ProjectJson</c> in Core instead.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HealthReport))]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(ProjectSummary))]
[JsonSerializable(typeof(ProjectListing))]
[JsonSerializable(typeof(SaveResult))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(DuplicateProjectRequest))]
[JsonSerializable(typeof(string[]))]
public sealed partial class ServerJsonContext : JsonSerializerContext
{
}
