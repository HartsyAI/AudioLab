using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Core.Tests;

// Each context is the strict one with a single rule loosened; the registry must refuse every one of them.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(NothingCommand))]
internal sealed partial class CaseInsensitiveCommandJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(NothingCommand))]
internal sealed partial class TrailingCommaCommandJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(NothingCommand))]
internal sealed partial class CommentCommandJsonContext : JsonSerializerContext;
