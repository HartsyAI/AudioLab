using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets how a track's lanes behave during playback.</summary>
public sealed record SetAutomationMode : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>read, touch, latch, write or off.</summary>
    [Description("read, touch, latch, write or off.")]
    public required AutomationMode Mode { get; init; }
}
