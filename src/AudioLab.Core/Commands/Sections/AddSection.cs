using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds a section at a start, cut short at the next one; the server allocates its id.</summary>
public sealed record AddSection : Command
{
    /// <summary>Where it starts, in seconds; not inside another section.</summary>
    [Description("Where it starts, in seconds; not inside another section.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Start { get; init; }

    /// <summary>How long it runs, at least 0.1 seconds; four bars at the tempo there when absent.</summary>
    [Description("How long it runs, at least 0.1 seconds; four bars at the tempo there when absent.")]
    [Range(Section.MinLength, ProjectLimits.MaxSeconds)]
    public double? Length { get; init; }

    /// <summary>Its name, up to 200 characters; "Section N" when absent or blank.</summary>
    [Description("Its name, up to 200 characters; \"Section N\" when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }

    /// <summary>Its colour as #rgb, #rrggbb or #rrggbbaa; the next palette colour when absent.</summary>
    [Description("Its colour as #rgb, #rrggbb or #rrggbbaa; the next palette colour when absent.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }
}
