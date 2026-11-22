using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Switches the live input to be heard through the track, or off; an aux bus has no input.</summary>
public sealed record SetTrackMonitor : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>True to hear the input through the track's inserts, fader and pan.</summary>
    [Description("True to hear the input through the track's inserts, fader and pan.")]
    public required bool Monitor { get; init; }
}
