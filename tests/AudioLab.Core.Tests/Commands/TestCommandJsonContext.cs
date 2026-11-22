using System.Text.Json.Serialization;

namespace AudioLab.Core.Tests;

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
[JsonSerializable(typeof(RenameProjectCommand))]
[JsonSerializable(typeof(SetBpmCommand))]
[JsonSerializable(typeof(AddMarkerCommand))]
[JsonSerializable(typeof(NothingCommand))]
[JsonSerializable(typeof(RefusedCommand))]
[JsonSerializable(typeof(HijackCommand))]
[JsonSerializable(typeof(BreakCommand))]
[JsonSerializable(typeof(UnregisteredCommand))]
[JsonSerializable(typeof(UnsealedCommand))]
[JsonSerializable(typeof(ShapeCommand))]
internal sealed partial class TestCommandJsonContext : JsonSerializerContext;
