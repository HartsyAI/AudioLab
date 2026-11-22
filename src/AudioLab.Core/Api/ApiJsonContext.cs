using System.Text.Json.Serialization;

namespace AudioLab.Core.Api;

/// <summary>Source-generated JSON for the records of the project routes, for the server and for the UI's typed client alike.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(ProjectSummary))]
[JsonSerializable(typeof(ProjectListing))]
[JsonSerializable(typeof(SaveResult))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(DuplicateProjectRequest))]
public sealed partial class ApiJsonContext : JsonSerializerContext
{
}
