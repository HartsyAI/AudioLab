using System.Text.Json.Serialization;

namespace AudioLab.Core.Api.Decoder;

/// <summary>Source-generated JSON for the decoder routes' records, for the server and the UI's typed client alike.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(DecoderStatus))]
[JsonSerializable(typeof(DecoderPackage))]
[JsonSerializable(typeof(DecoderInstallRequest))]
public sealed partial class DecoderJsonContext : JsonSerializerContext
{
}
