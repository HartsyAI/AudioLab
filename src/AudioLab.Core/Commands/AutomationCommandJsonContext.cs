using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the automation commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(PointValue))]
[JsonSerializable(typeof(WrittenPoint))]
[JsonSerializable(typeof(AddAutomationPoint))]
[JsonSerializable(typeof(MoveAutomationPoint))]
[JsonSerializable(typeof(DeleteAutomationPoint))]
[JsonSerializable(typeof(SetAutomationValues))]
[JsonSerializable(typeof(CommitAutomationWrite))]
[JsonSerializable(typeof(SetAutomationMode))]
[JsonSerializable(typeof(SetAutomationView))]
public sealed partial class AutomationCommandJsonContext : JsonSerializerContext;
