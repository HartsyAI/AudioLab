using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Adds an effect at the end of a track's insert chain with its starting settings, or a preset's; the server allocates its id.</summary>
public sealed record AddEffect : Command
{
    /// <summary>The track whose chain gets the effect; a bus holds effects too.</summary>
    [Description("The track whose chain gets the effect; a bus holds effects too.")]
    public required TrackId Track { get; init; }

    /// <summary>eq, compressor, reverb, delay or saturation.</summary>
    [Description("eq, compressor, reverb, delay or saturation.")]
    public required EffectKind Kind { get; init; }

    /// <summary>A preset of that kind to start from; the kind's defaults when absent.</summary>
    [Description("A preset of that kind to start from; the kind's defaults when absent.")]
    [AllowedValues(
        "Vocal clarity", "Drum punch", "Bass focus", "Telephone", "Vocal level", "Drum smash",
        "Glue (gentle)", "Small room", "Hall", "Cathedral", "Slap", "Quarter echo", "Dotted 8th", "Warm",
        "Crunch", "Destroy")]
    public string? Preset { get; init; }
}
