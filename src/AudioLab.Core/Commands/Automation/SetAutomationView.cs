using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Shows or hides a track's automation lane and chooses which lane it shows.</summary>
public sealed record SetAutomationView : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>Whether the lane is open under the track; it keeps its state when absent.</summary>
    [Description("Whether the lane is open under the track; it keeps its state when absent.")]
    public bool? Visible { get; init; }

    /// <summary>Which lane the track shows, volume or pan; it keeps its state when absent.</summary>
    [Description("Which lane the track shows, volume or pan; it keeps its state when absent.")]
    public AutomationParameter? Parameter { get; init; }
}
