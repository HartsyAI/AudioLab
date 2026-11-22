using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds a marker; the server allocates its id.</summary>
public sealed record AddMarker : Command
{
    /// <summary>Where it sits, in seconds; not before the start.</summary>
    [Description("Where it sits, in seconds; not before the start.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Time { get; init; }

    /// <summary>Its name, up to 200 characters; "Marker N" when absent or blank.</summary>
    [Description("Its name, up to 200 characters; \"Marker N\" when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }
}
