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

    /// <summary>The setting's name, which must belong to the effect's kind.</summary>
    /// <remarks>
    /// for example threshold for a compressor, midFreq for an eq, mix for a reverb, delay, or saturation.
    /// </remarks>
    [Description(
        "The setting's name, which must belong to the effect's kind: for example threshold for a compressor, " +
        "midFreq for an eq, mix for a reverb, delay, or saturation.")]
    [AllowedValues(
        "hpFreq", "lowGain", "lowFreq", "midGain", "midFreq", "midQ", "highGain", "highFreq", "lpFreq",
        "threshold", "knee", "ratio", "attack", "release", "makeup", "size", "decay", "predelay", "mix",
        "time", "feedback", "tone", "drive", "trim")]
    public required string Parameter { get; init; }

    /// <summary>The new value, inside the setting's range.</summary>
    /// <remarks>
    /// eq: hpFreq 20 to 1000, lowGain -12 to 12, lowFreq 40 to 600, midGain -12 to 12, midFreq 200 to 8000, midQ
    /// 0.3 to 4, highGain -12 to 12, highFreq 1000 to 16000, lpFreq 1000 to 20000; compressor: threshold -60 to
    /// 0, knee 0 to 40, ratio 1 to 20, attack 0.001 to 1, release 0.01 to 1, makeup 0 to 24; reverb: size 0.1 to
    /// 8, decay 0.5 to 6, predelay 0 to 0.2, mix 0 to 1; delay: time 0.02 to 2, feedback 0 to 0.9, tone 500 to
    /// 15000, mix 0 to 1; saturation: drive 0 to 1, trim -12 to 6, mix 0 to 1.
    /// </remarks>
    [Description(
        "The new value, inside the setting's range: eq: hpFreq 20 to 1000, lowGain -12 to 12, lowFreq 40 to " +
        "600, midGain -12 to 12, midFreq 200 to 8000, midQ 0.3 to 4, highGain -12 to 12, highFreq 1000 to " +
        "16000, lpFreq 1000 to 20000; compressor: threshold -60 to 0, knee 0 to 40, ratio 1 to 20, attack " +
        "0.001 to 1, release 0.01 to 1, makeup 0 to 24; reverb: size 0.1 to 8, decay 0.5 to 6, predelay 0 to " +
        "0.2, mix 0 to 1; delay: time 0.02 to 2, feedback 0 to 0.9, tone 500 to 15000, mix 0 to 1; " +
        "saturation: drive 0 to 1, trim -12 to 6, mix 0 to 1.")]
    public required double Value { get; init; }
}
