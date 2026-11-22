using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Recolours a group.</summary>
public sealed record SetGroupColor : Command
{
    /// <summary>The group to colour.</summary>
    [Description("The group to colour.")]
    public required GroupId Group { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public required string Color { get; init; }
}
