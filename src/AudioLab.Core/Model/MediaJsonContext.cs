using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Source-generated JSON for the media routes, strict on what the page reads: no unknown member, no missing or null one.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(MediaSummary))]
[JsonSerializable(typeof(MediaListing))]
[JsonSerializable(typeof(MediaLimits))]
public sealed partial class MediaJsonContext : JsonSerializerContext
{
}
