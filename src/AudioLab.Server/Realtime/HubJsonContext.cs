using System.Text.Json.Serialization;

namespace AudioLab.Server.Realtime;

/// <summary>Source-generated JSON for the hub's messages, for the server and for a .NET client alike. Documents travel as <c>ProjectJson</c> text inside them.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(ProjectChange))]
[JsonSerializable(typeof(JoinReply))]
[JsonSerializable(typeof(ProjectClosed))]
[JsonSerializable(typeof(CommandReply))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
public sealed partial class HubJsonContext : JsonSerializerContext
{
}
