using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds an empty MIDI clip to an instrument track; the server allocates the clip id.</summary>
public sealed record AddMidiClip : Command
{
    /// <summary>The instrument track to put the clip on.</summary>
    [Description("The instrument track to put the clip on.")]
    public required TrackId Track { get; init; }

    /// <summary>Where the clip starts on the timeline, in seconds.</summary>
    [Description("Where the clip starts on the timeline, in seconds.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Start { get; init; }

    /// <summary>How long the clip is, in seconds, at least 0.05.</summary>
    [Description("How long the clip is, in seconds, at least 0.05.")]
    [Range(MidiClip.MinLength, ProjectLimits.MaxSeconds)]
    public required double Length { get; init; }

    /// <summary>The clip's name, up to 200 characters; "MIDI N" when absent or blank.</summary>
    [Description("The clip's name, up to 200 characters; \"MIDI N\" when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track colour.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track colour.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }
}
