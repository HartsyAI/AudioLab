using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the drum pattern commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(AddBeatLane))]
[JsonSerializable(typeof(RemoveBeatLane))]
[JsonSerializable(typeof(MoveBeatLane))]
[JsonSerializable(typeof(RenameBeatLane))]
[JsonSerializable(typeof(SetBeatLaneGain))]
[JsonSerializable(typeof(SetBeatLaneMedia))]
[JsonSerializable(typeof(SetBeatLaneSteps))]
[JsonSerializable(typeof(SetBeatSteps))]
[JsonSerializable(typeof(SetBeatSwing))]
public sealed partial class BeatCommandJsonContext : JsonSerializerContext;
