using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the instrument commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(SetInstrumentPreset))]
[JsonSerializable(typeof(SetInstrumentParameter))]
[JsonSerializable(typeof(SetOscillatorWave))]
[JsonSerializable(typeof(SetSamplerRootNote))]
[JsonSerializable(typeof(SetSamplerOneShot))]
[JsonSerializable(typeof(SetSamplerSample))]
public sealed partial class InstrumentCommandJsonContext : JsonSerializerContext;
