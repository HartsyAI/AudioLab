using System.Text.Json.Serialization;

namespace AudioLab.Server.Realtime;

/// <summary>A new revision of a project, sent to every client that joined it, whoever made the change.</summary>
/// <param name="Project">The project's id.</param>
/// <param name="Revision">The revision, which rises by one with every change that alters the document.</param>
/// <param name="Document">The whole document as <c>ProjectJson</c> writes it; read it back with <c>ProjectJson.Read</c>, which is as strict as the server.</param>
/// <param name="Reason">What caused it: the wire name of the command, or undo or redo.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectChange(string Project, long Revision, string Document, string Reason);
