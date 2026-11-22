using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Recolours a section.</summary>
public sealed record SetSectionColor : Command
{
    /// <summary>The section to colour.</summary>
    [Description("The section to colour.")]
    public required SectionId Section { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public required string Color { get; init; }
}
