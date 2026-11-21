using System.Text.Json.Serialization;

namespace AudioLab.Core.Migration;

/// <summary>The strict JSON contract for reading a version 3 project: a member the extension never wrote is an error.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(V3Project))]
internal sealed partial class V3JsonContext : JsonSerializerContext;
