using System.Text.Json.Serialization;

namespace AudioLab.Core.Transcription;

/// <summary>Source-generated JSON for the transcription files and routes, strict like the media context: no unknown member.</summary>
[JsonSourceGenerationOptions(
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(TranscriptDocument))]
[JsonSerializable(typeof(TranscriptionJobFile))]
[JsonSerializable(typeof(ChunkRecord))]
[JsonSerializable(typeof(TranscriptionStatus))]
public sealed partial class TranscriptJsonContext : JsonSerializerContext
{
}
