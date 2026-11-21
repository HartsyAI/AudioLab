namespace AudioLab.Core.Model;

/// <summary>How a track draws in the arrangement.</summary>
public sealed record TrackView
{
    /// <summary>The track's lane height in pixels at the default zoom.</summary>
    public required double Height { get; init; }

    /// <summary>Whether the automation lane is open under the track.</summary>
    public required bool AutomationVisible { get; init; }

    /// <summary>Which automation lane the track shows.</summary>
    public required AutomationParameter AutomationParameter { get; init; }
}
