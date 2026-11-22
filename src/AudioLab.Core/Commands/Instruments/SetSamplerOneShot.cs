using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets whether a sampler plays the whole sample regardless of how long a note is held.</summary>
public sealed record SetSamplerOneShot : Command
{
    /// <summary>The instrument track to edit.</summary>
    [Description("The instrument track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>True to play the whole sample.</summary>
    [Description("True to play the whole sample.")]
    public required bool OneShot { get; init; }
}
