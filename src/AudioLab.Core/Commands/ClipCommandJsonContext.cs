using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the clip commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(AddClip))]
[JsonSerializable(typeof(AddMidiClip))]
[JsonSerializable(typeof(MoveClip))]
[JsonSerializable(typeof(TrimClip))]
[JsonSerializable(typeof(SplitClip))]
[JsonSerializable(typeof(DuplicateClip))]
[JsonSerializable(typeof(DeleteClip))]
[JsonSerializable(typeof(SetClipGain))]
[JsonSerializable(typeof(SetClipFades))]
[JsonSerializable(typeof(SetMidiClipLength))]
[JsonSerializable(typeof(SetClipMuted))]
[JsonSerializable(typeof(RenameClip))]
[JsonSerializable(typeof(SetClipColor))]
[JsonSerializable(typeof(ReorderClip))]
public sealed partial class ClipCommandJsonContext : JsonSerializerContext;
