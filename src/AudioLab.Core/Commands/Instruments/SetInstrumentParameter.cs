using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets one numeric setting of an instrument; the range depends on the setting.</summary>
public sealed record SetInstrumentParameter : Command
{
    /// <summary>The instrument track to edit.</summary>
    [Description("The instrument track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>The setting's name, which must belong to the instrument's kind.</summary>
    /// <remarks>
    /// a synth has them all, a sampler only attack, release and gain.
    /// </remarks>
    [Description(
        "The setting's name, which must belong to the instrument's kind: a synth has them all, a sampler only " +
        "attack, release and gain.")]
    [AllowedValues(
        "mix2", "detune", "cutoff", "resonance", "filterEnv", "attack", "decay", "sustain", "release",
        "gain")]
    public required string Parameter { get; init; }

    /// <summary>The new value, inside the setting's range.</summary>
    /// <remarks>
    /// mix2 0 to 1, detune 0 to 50, cutoff 100 to 12000, resonance 0.3 to 15, filterEnv 0 to 3, attack 0.001 to
    /// 2, decay 0.01 to 3, sustain 0 to 1, release 0.01 to 4 (a synth and a sampler alike), gain 0 to 1.
    /// </remarks>
    [Description(
        "The new value, inside the setting's range: mix2 0 to 1, detune 0 to 50, cutoff 100 to 12000, " +
        "resonance 0.3 to 15, filterEnv 0 to 3, attack 0.001 to 2, decay 0.01 to 3, sustain 0 to 1, release " +
        "0.01 to 4 (a synth and a sampler alike), gain 0 to 1.")]
    public required double Value { get; init; }
}
