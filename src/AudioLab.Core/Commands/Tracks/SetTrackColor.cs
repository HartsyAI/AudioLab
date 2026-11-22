using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Recolours a track and the clips that follow its colour.</summary>
public sealed record SetTrackColor : Command
{
    /// <summary>The track to colour.</summary>
    [Description("The track to colour.")]
    public required TrackId Track { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public required string Color { get; init; }
}
