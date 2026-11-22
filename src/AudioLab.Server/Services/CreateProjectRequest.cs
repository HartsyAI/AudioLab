using System.Text.Json.Serialization;

namespace AudioLab.Server.Services;

/// <summary>The body of a request to create a project. The owner is never part of it: it comes from the door.</summary>
/// <param name="Name">The new project's name.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateProjectRequest(string Name);
