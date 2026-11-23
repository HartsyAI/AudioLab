using System.Text.Json.Serialization;

namespace AudioLab.Core.Realtime;

/// <summary>The answer to joining a project: its state at the moment the client became a member.</summary>
/// <param name="Project">The project's id.</param>
/// <param name="Revision">The revision of <paramref name="Document"/>; every later change is sent with a higher one, and none is missed.</param>
/// <param name="Document">The whole document as <c>ProjectJson</c> writes it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JoinReply(string Project, long Revision, string Document);
