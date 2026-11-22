using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Gives a marker a colour, or the default colour when none is given.</summary>
public sealed record SetMarkerColor : Command
{
    /// <summary>The marker to colour.</summary>
    [Description("The marker to colour.")]
    public required MarkerId Marker { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; absent for the default.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa; absent for the default.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }
}
