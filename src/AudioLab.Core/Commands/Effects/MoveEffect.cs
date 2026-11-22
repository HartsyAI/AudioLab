using System.ComponentModel;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves an effect one step earlier or later in the chain, which changes how it sounds; at the end of the chain nothing changes.</summary>
public sealed record MoveEffect : Command
{
    /// <summary>The track that holds the effect.</summary>
    [Description("The track that holds the effect.")]
    public required TrackId Track { get; init; }

    /// <summary>The effect to move.</summary>
    [Description("The effect to move.")]
    public required EffectId Effect { get; init; }

    /// <summary>up is earlier in the chain, down is later.</summary>
    [Description("up is earlier in the chain, down is later.")]
    public required MoveDirection Direction { get; init; }
}
