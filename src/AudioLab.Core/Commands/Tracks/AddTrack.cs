using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds an empty track at the bottom; the server allocates its id.</summary>
public sealed record AddTrack : Command
{
    /// <summary>audio, instrument or bus; audio when absent.</summary>
    [Description("audio, instrument or bus; audio when absent.")]
    public TrackKind? Kind { get; init; }

    /// <summary>The track's name, up to 200 characters; "Track N", "Bus N" or the instrument's name followed by a number when absent or blank.</summary>
    [Description("The track's name, up to 200 characters; \"Track N\", \"Bus N\" or the instrument's name followed by a number when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }

    /// <summary>Its colour as #rgb, #rrggbb or #rrggbbaa; the next track colour when absent.</summary>
    [Description("Its colour as #rgb, #rrggbb or #rrggbbaa; the next track colour when absent.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }

    /// <summary>For an instrument track, the sound it starts with: a synth preset or Sampler; Soft Keys when absent.</summary>
    [Description("For an instrument track, the sound it starts with: a synth preset or Sampler; Soft Keys when absent.")]
    [AllowedValues("Soft Keys", "Warm Pad", "Sub Bass", "Saw Lead", "Pluck", "Organ", "Bell", "Sampler")]
    public string? Preset { get; init; }
}
