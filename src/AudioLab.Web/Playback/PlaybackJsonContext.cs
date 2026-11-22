using System.Text.Json.Serialization;

namespace AudioLab.Web.Playback;

/// <summary>Source-generated JSON for the reports and for the worklet's counters.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RunReport))]
[JsonSerializable(typeof(WorkletStats))]
public sealed partial class PlaybackJsonContext : JsonSerializerContext
{
}
