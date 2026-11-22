using System.Text.Json.Serialization;

namespace AudioLab.Audio;

/// <summary>Source-generated JSON for the records the engine hosts report.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EngineSnapshot))]
[JsonSerializable(typeof(ClipLoadResult))]
public sealed partial class AudioJsonContext : JsonSerializerContext
{
}
