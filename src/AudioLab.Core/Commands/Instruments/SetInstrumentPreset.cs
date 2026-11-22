using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Gives an instrument track a synth preset or an empty sampler, replacing its sound.</summary>
public sealed record SetInstrumentPreset : Command
{
    /// <summary>The instrument track to edit.</summary>
    [Description("The instrument track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>The preset: one of the synth presets or Sampler.</summary>
    [Description("The preset: one of the synth presets or Sampler.")]
    [AllowedValues("Soft Keys", "Warm Pad", "Sub Bass", "Saw Lead", "Pluck", "Organ", "Bell", "Sampler")]
    public required string Preset { get; init; }
}
