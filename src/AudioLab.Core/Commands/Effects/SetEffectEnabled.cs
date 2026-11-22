using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Switches an effect on or bypasses it.</summary>
public sealed record SetEffectEnabled : Command
{
    /// <summary>The track that holds the effect.</summary>
    [Description("The track that holds the effect.")]
    public required TrackId Track { get; init; }

    /// <summary>The effect to switch.</summary>
    [Description("The effect to switch.")]
    public required EffectId Effect { get; init; }

    /// <summary>True to put the effect in the signal path, false to bypass it.</summary>
    [Description("True to put the effect in the signal path, false to bypass it.")]
    public required bool Enabled { get; init; }
}
