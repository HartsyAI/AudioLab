using System.Text.Json.Serialization;

namespace AudioLab.Server.Services;

/// <summary>The body of a request to duplicate a project as a new one.</summary>
/// <param name="Name">The copy's name; "Copy of" and the original's name when not given.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DuplicateProjectRequest(string? Name = null);
