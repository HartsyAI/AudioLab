using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the effect commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(AddEffect))]
[JsonSerializable(typeof(RemoveEffect))]
[JsonSerializable(typeof(MoveEffect))]
[JsonSerializable(typeof(SetEffectEnabled))]
[JsonSerializable(typeof(SetEffectParameter))]
[JsonSerializable(typeof(SetDelaySync))]
[JsonSerializable(typeof(ApplyEffectPreset))]
public sealed partial class EffectCommandJsonContext : JsonSerializerContext;
