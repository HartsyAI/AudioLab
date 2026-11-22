namespace AudioLab.Core.Commands;

/// <summary>Adds a section at a start, cut short at the next one; the server allocates its id.</summary>
public sealed record AddSection : Command
{
    /// <summary>Where it starts, in seconds; not inside another section.</summary>
    public required double Start { get; init; }

    /// <summary>How long it runs; four bars at the tempo there when absent.</summary>
    public double? Length { get; init; }

    /// <summary>Its name; "Section N" when absent.</summary>
    public string? Name { get; init; }

    /// <summary>Its colour; the next palette colour when absent.</summary>
    public string? Color { get; init; }
}
