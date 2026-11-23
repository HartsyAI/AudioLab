using System.Text.Json.Serialization;

namespace AudioLab.Core.Playback;

/// <summary>Source-generated JSON for what the engine's worker and the audio worklet report to the page.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(HostStatus))]
[JsonSerializable(typeof(WorkletStats))]
public sealed partial class PlaybackJsonContext : JsonSerializerContext
{
}
