using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Gives a clip a colour of its own, or lets it follow its track colour when none is given.</summary>
public sealed record SetClipColor : Command
{
    /// <summary>The clip to colour.</summary>
    [Description("The clip to colour.")]
    public required ClipId Clip { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }
}
