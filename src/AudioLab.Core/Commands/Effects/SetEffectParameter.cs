using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets one numeric setting of an effect; the range depends on the setting.</summary>
public sealed record SetEffectParameter : Command
{
    /// <summary>The track that holds the effect.</summary>
    [Description("The track that holds the effect.")]
    public required TrackId Track { get; init; }

    /// <summary>The effect to edit.</summary>
    [Description("The effect to edit.")]
    public required EffectId Effect { get; init; }

    /// <summary>The setting's name, which must belong to the effect's kind: for example threshold for a compressor, midFreq for an eq, mix for a reverb, delay, or saturation.</summary>
    [Description("The setting's name, which must belong to the effect's kind: for example threshold for a compressor, midFreq for an eq, mix for a reverb, delay, or saturation.")]
    [AllowedValues("hpFreq", "lowGain", "lowFreq", "midGain", "midFreq", "midQ", "highGain", "highFreq", "lpFreq", "threshold", "knee", "ratio", "attack", "release", "makeup", "size", "decay", "predelay", "mix", "time", "feedback", "tone", "drive", "trim")]
    public required string Parameter { get; init; }

    /// <summary>The new value, inside the setting's range.</summary>
    [Description("The new value, inside the setting's range.")]
    public required double Value { get; init; }
}
