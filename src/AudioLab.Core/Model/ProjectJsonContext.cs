using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The source-generated JSON contract for project documents; unknown, missing or null members are errors.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(ProjectDocument))]
[JsonSerializable(typeof(MediaRecord))]
public sealed partial class ProjectJsonContext : JsonSerializerContext;
