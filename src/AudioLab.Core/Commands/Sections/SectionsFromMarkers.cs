namespace AudioLab.Core.Commands;

/// <summary>Replaces every section with one per marker; the server allocates new ids, in order, each time it runs.</summary>
public sealed record SectionsFromMarkers : Command;
