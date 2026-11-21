namespace AudioLab.Core.Model;

/// <summary>A named point on the timeline.</summary>
public sealed record Marker
{
    /// <summary>The marker's id.</summary>
    public required MarkerId Id { get; init; }

    /// <summary>Where it sits, in seconds.</summary>
    public required double Time { get; init; }

    /// <summary>The marker's name.</summary>
    public required string Name { get; init; }

    /// <summary>A colour, or null for the default.</summary>
    public string? Color { get; init; }
}
