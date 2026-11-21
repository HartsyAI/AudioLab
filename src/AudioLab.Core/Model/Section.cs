namespace AudioLab.Core.Model;

/// <summary>A named, coloured span of the song such as Intro or Drop. Sections never overlap.</summary>
public sealed record Section
{
    /// <summary>The shortest a section may be, in seconds.</summary>
    public const double MinLength = 0.1;

    /// <summary>The section's id.</summary>
    public required SectionId Id { get; init; }

    /// <summary>Where it starts, in seconds.</summary>
    public required double Start { get; init; }

    /// <summary>Where it ends, in seconds.</summary>
    public required double End { get; init; }

    /// <summary>The section's name.</summary>
    public required string Name { get; init; }

    /// <summary>The section's colour.</summary>
    public required string Color { get; init; }
}
