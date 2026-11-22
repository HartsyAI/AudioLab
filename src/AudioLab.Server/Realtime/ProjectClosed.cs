using System.Text.Json.Serialization;

namespace AudioLab.Server.Realtime;

/// <summary>A project's group has ended.</summary>
/// <param name="Project">The project's id.</param>
/// <param name="Reason">Why: deleted.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectClosed(string Project, string Reason);
