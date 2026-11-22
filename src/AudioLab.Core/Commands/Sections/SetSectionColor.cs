using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Recolours a section.</summary>
public sealed record SetSectionColor : Command
{
    /// <summary>The section to colour.</summary>
    public required SectionId Section { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa.</summary>
    public required string Color { get; init; }
}
