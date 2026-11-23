using System.Text.Json.Serialization;

namespace AudioLab.Web.Services;

/// <summary>Source-generated JSON for the records only the UI reads; the shared ones are in Core's JSON contexts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ApiProblem))]
public sealed partial class WebJsonContext : JsonSerializerContext
{
}
