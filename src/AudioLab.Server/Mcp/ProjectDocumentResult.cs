using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLab.Server.Mcp;

/// <summary>A project's document at a revision, as get_project and the project resource return it.</summary>
/// <param name="Revision">The revision the document is at.</param>
/// <param name="Document">The document, exactly as <c>ProjectJson</c> writes it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectDocumentResult(long Revision, JsonElement Document);
