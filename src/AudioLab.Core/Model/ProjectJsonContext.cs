using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The source-generated JSON contract for project documents; unknown or missing members are errors.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(ProjectDocument))]
[JsonSerializable(typeof(MediaRecord))]
[JsonSerializable(typeof(TempoChange))]
[JsonSerializable(typeof(Marker))]
[JsonSerializable(typeof(Section))]
[JsonSerializable(typeof(TrackGroup))]
[JsonSerializable(typeof(Track))]
[JsonSerializable(typeof(BeatLane))]
[JsonSerializable(typeof(AudioClip))]
[JsonSerializable(typeof(MidiClip))]
[JsonSerializable(typeof(Effect))]
[JsonSerializable(typeof(Send))]
[JsonSerializable(typeof(AutomationPoint))]
[JsonSerializable(typeof(MidiNote))]
[JsonSerializable(typeof(MidiControlChange))]
[JsonSerializable(typeof(LyricWord))]
[JsonSerializable(typeof(bool))]
public sealed partial class ProjectJsonContext : JsonSerializerContext;
