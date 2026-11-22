using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the project settings commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(RenameProject))]
[JsonSerializable(typeof(SetMetronome))]
[JsonSerializable(typeof(SetProjectView))]
public sealed partial class ProjectCommandJsonContext : JsonSerializerContext;
