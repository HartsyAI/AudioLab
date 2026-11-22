using System.Text.Json.Serialization;

namespace AudioLab.Core.Realtime;

/// <summary>The answer to a command, an undo or a redo, to the client that sent it. Everyone, the sender included, also hears <c>DocumentChanged</c>.</summary>
/// <param name="Succeeded">Whether it was applied; a refused command changes nothing and has errors.</param>
/// <param name="Changed">Whether it raised the revision; a command that changes nothing is applied and does not.</param>
/// <param name="Revision">The project's revision after the call.</param>
/// <param name="Errors">Why it was refused; empty when it succeeded.</param>
/// <param name="Created">The ids the command allocated, in the order its description names them.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CommandReply(bool Succeeded, bool Changed, long Revision, HubCommandError[] Errors, string[] Created);
