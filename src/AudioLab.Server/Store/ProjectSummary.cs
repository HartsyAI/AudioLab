using System.Text.Json.Serialization;

namespace AudioLab.Server.Store;

/// <summary>One project in a user's list.</summary>
/// <param name="Id">The project's id.</param>
/// <param name="Name">The project's name.</param>
/// <param name="Modified">When the project last changed.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectSummary(string Id, string Name, DateTimeOffset Modified);
