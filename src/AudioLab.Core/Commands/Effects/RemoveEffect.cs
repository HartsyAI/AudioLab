using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes an effect from a track's insert chain.</summary>
public sealed record RemoveEffect : Command
{
    /// <summary>The track that holds the effect.</summary>
    [Description("The track that holds the effect.")]
    public required TrackId Track { get; init; }

    /// <summary>The effect to remove.</summary>
    [Description("The effect to remove.")]
    public required EffectId Effect { get; init; }
}
