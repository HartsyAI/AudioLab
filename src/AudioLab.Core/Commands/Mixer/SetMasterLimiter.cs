using System.ComponentModel;

namespace AudioLab.Core.Commands;

/// <summary>Switches the master limiter on or off.</summary>
public sealed record SetMasterLimiter : Command
{
    /// <summary>True to turn the limiter on.</summary>
    [Description("True to turn the limiter on.")]
    public required bool Enabled { get; init; }
}
