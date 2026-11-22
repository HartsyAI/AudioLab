using System.Text.Json.Serialization;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the lyrics commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(HeardWord))]
[JsonSerializable(typeof(SetLyricsText))]
[JsonSerializable(typeof(ApplyLyricsAlignment))]
[JsonSerializable(typeof(ApplyLyricsTranscript))]
[JsonSerializable(typeof(EstimateLyricsTiming))]
[JsonSerializable(typeof(ClearLyricsTiming))]
[JsonSerializable(typeof(ShiftLyrics))]
[JsonSerializable(typeof(SetLyricsWordTime))]
[JsonSerializable(typeof(ImportLyricsLrc))]
public sealed partial class LyricsCommandJsonContext : JsonSerializerContext;
