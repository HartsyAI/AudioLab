using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>The strict JSON contract for a version 3 project: a member the extension never wrote is an error.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false,
    RespectNullableAnnotations = true,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(V3Project))]
[JsonSerializable(typeof(LyricWord))]
internal sealed partial class V3JsonContext : JsonSerializerContext;
