namespace AudioLab.Core.Commands;

/// <summary>Adds a marker; the server allocates its id.</summary>
public sealed record AddMarker : Command
{
    /// <summary>Where it sits, in seconds.</summary>
    public required double Time { get; init; }

    /// <summary>Its name; "Marker N" when absent.</summary>
    public string? Name { get; init; }
}
