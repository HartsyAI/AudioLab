namespace AudioLab.Core.Commands;

/// <summary>Replaces every section with one per marker; the server allocates their ids, in order.</summary>
public sealed record SectionsFromMarkers : Command;
