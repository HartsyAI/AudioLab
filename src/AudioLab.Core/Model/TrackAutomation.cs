namespace AudioLab.Core.Model;

/// <summary>A track's volume and pan envelopes and how they are used.</summary>
public sealed record TrackAutomation
{
    /// <summary>How the lanes behave during playback.</summary>
    public required AutomationMode Mode { get; init; }

    /// <summary>The volume envelope, by time.</summary>
    public required IReadOnlyList<AutomationPoint> Volume { get; init; }

    /// <summary>The pan envelope, by time.</summary>
    public required IReadOnlyList<AutomationPoint> Pan { get; init; }
}
