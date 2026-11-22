using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the group, track and mixer commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(CreateGroup))]
[JsonSerializable(typeof(AssignToGroup))]
[JsonSerializable(typeof(Ungroup))]
[JsonSerializable(typeof(RenameGroup))]
[JsonSerializable(typeof(SetGroupColor))]
[JsonSerializable(typeof(SetGroupCollapsed))]
[JsonSerializable(typeof(SetGroupTint))]
[JsonSerializable(typeof(SetGroupMuted))]
[JsonSerializable(typeof(SetGroupSoloed))]
[JsonSerializable(typeof(SetGroupVolume))]
[JsonSerializable(typeof(MoveGroup))]
[JsonSerializable(typeof(AddTrack))]
[JsonSerializable(typeof(RemoveTrack))]
[JsonSerializable(typeof(RenameTrack))]
[JsonSerializable(typeof(SetTrackColor))]
[JsonSerializable(typeof(MoveTrack))]
[JsonSerializable(typeof(SetTrackVolume))]
[JsonSerializable(typeof(SetTrackPan))]
[JsonSerializable(typeof(SetTrackMuted))]
[JsonSerializable(typeof(SetTrackSoloed))]
[JsonSerializable(typeof(SetMasterVolume))]
[JsonSerializable(typeof(SetMasterLimiter))]
[JsonSerializable(typeof(AddSend))]
[JsonSerializable(typeof(RemoveSend))]
[JsonSerializable(typeof(SetSendLevel))]
[JsonSerializable(typeof(SetSendPre))]
[JsonSerializable(typeof(SetSendMuted))]
public sealed partial class TrackCommandJsonContext : JsonSerializerContext;
