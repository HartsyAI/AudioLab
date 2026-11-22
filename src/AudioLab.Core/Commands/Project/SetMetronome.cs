using System.ComponentModel;

namespace AudioLab.Core.Commands;

/// <summary>Switches the metronome click on or off.</summary>
public sealed record SetMetronome : Command
{
    /// <summary>True for the metronome to click while the transport runs.</summary>
    [Description("True for the metronome to click while the transport runs.")]
    public required bool Enabled { get; init; }
}
