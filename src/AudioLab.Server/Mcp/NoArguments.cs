using System.Text.Json.Serialization;

namespace AudioLab.Server.Mcp;

/// <summary>The input of a tool that takes none.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NoArguments;
