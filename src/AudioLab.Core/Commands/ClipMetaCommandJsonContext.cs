using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the clip metadata commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(SetClipScore))]
[JsonSerializable(typeof(SetScoreLabel))]
[JsonSerializable(typeof(ClearClipScore))]
[JsonSerializable(typeof(SetClipStem))]
[JsonSerializable(typeof(ClearClipStem))]
public sealed partial class ClipMetaCommandJsonContext : JsonSerializerContext;
