using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Source-generated JSON for the media routes.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MediaSummary))]
[JsonSerializable(typeof(MediaListing))]
[JsonSerializable(typeof(MediaLimits))]
public sealed partial class MediaJsonContext : JsonSerializerContext
{
}
