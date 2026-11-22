using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds an audio clip that plays a media file; the server allocates the clip id.</summary>
public sealed record AddClip : Command
{
    /// <summary>The audio track to put the clip on.</summary>
    [Description("The audio track to put the clip on.")]
    public required TrackId Track { get; init; }

    /// <summary>The media file the clip plays; its duration is the Duration given here.</summary>
    [Description("The media file the clip plays; its duration is the Duration given here.")]
    public required MediaId Media { get; init; }

    /// <summary>The length of the whole media file, in seconds, above 0.</summary>
    [Description("The length of the whole media file, in seconds, above 0.")]
    [Range(ProjectLimits.Tolerance, ProjectLimits.MaxSeconds)]
    public required double Duration { get; init; }

    /// <summary>Where the clip starts on the timeline, in seconds; the end of the track's last clip when absent.</summary>
    [Description("Where the clip starts on the timeline, in seconds; the end of the track's last clip when absent.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? Start { get; init; }

    /// <summary>The clip's name, up to 200 characters; "Clip N" when absent or blank.</summary>
    [Description("The clip's name, up to 200 characters; \"Clip N\" when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track colour.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track colour.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }
}
