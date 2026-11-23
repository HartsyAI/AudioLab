using System.Text.Json.Serialization;

namespace AudioLab.Server.Tests;

// The same strict rules as ProjectJsonContext, which the registry insists on.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(RenameCommand))]
[JsonSerializable(typeof(SetBpmCommand))]
[JsonSerializable(typeof(AddMarkerCommand))]
[JsonSerializable(typeof(RefuseCommand))]
[JsonSerializable(typeof(FailWithCommand))]
[JsonSerializable(typeof(BadBpmCommand))]
[JsonSerializable(typeof(NoChangeCommand))]
[JsonSerializable(typeof(AddManyCommand))]
[JsonSerializable(typeof(BulkCommand))]
[JsonSerializable(typeof(SlowCommand))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
internal sealed partial class TestCommandJson : JsonSerializerContext;
