using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the tempo, marker and section commands, as strict as the project document's.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(SetTempoBpm))]
[JsonSerializable(typeof(SetTimeSignature))]
[JsonSerializable(typeof(AddTempoChange))]
[JsonSerializable(typeof(SetTempoChangeBpm))]
[JsonSerializable(typeof(MoveTempoChange))]
[JsonSerializable(typeof(RemoveTempoChange))]
[JsonSerializable(typeof(AddMarker))]
[JsonSerializable(typeof(RenameMarker))]
[JsonSerializable(typeof(MoveMarker))]
[JsonSerializable(typeof(SetMarkerColor))]
[JsonSerializable(typeof(RemoveMarker))]
[JsonSerializable(typeof(AddSection))]
[JsonSerializable(typeof(SetSectionSpan))]
[JsonSerializable(typeof(RenameSection))]
[JsonSerializable(typeof(SetSectionColor))]
[JsonSerializable(typeof(RemoveSection))]
[JsonSerializable(typeof(ClearSections))]
[JsonSerializable(typeof(SectionsFromMarkers))]
public sealed partial class TimelineCommandJsonContext : JsonSerializerContext;
