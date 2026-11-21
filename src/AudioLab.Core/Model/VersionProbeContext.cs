using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The JSON contract for <see cref="VersionProbe"/>; it ignores every other member.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VersionProbe))]
internal sealed partial class VersionProbeContext : JsonSerializerContext;
