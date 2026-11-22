using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Everything the render loop needs to run one track or bus, worked out when the plan is prepared.</summary>
internal sealed class PlannedTrack
{
    /// <summary>The slot of this bus among the plan's buses, or -1 for a track.</summary>
    public required int BusSlot { get; init; }

    /// <summary>The fader times the group's fader, or zero when the track is muted.</summary>
    public required float FaderGain { get; init; }

    /// <summary>The track's own pan, from -1 to 1.</summary>
    public required double Pan { get; init; }

    /// <summary>Whether the direct output is heard once solo is counted; sends still run when it is not.</summary>
    public required bool SoloAudible { get; init; }

    /// <summary>The volume envelope, or null when the lane is empty or the mode ignores it.</summary>
    public required ValueList<AutomationPoint>? VolumeLane { get; init; }

    /// <summary>The pan envelope, an offset from <see cref="Pan"/>, or null when the lane is empty or the mode ignores it.</summary>
    public required ValueList<AutomationPoint>? PanLane { get; init; }

    /// <summary>The sends into buses, in the track's order.</summary>
    public required PlannedSend[] Sends { get; init; }

    /// <summary>The enabled inserts, in signal order.</summary>
    public required IInsert[] Inserts { get; init; }

    /// <summary>The heard stretches of the track's audio clips, ordered by start.</summary>
    public required PlannedClip[] Clips { get; init; }

    /// <summary>For each clip, the largest end frame among it and the clips before it, so a block finds its first clip by search.</summary>
    public required long[] LatestEnds { get; init; }

    /// <summary>The instrument's source, or null.</summary>
    public required ITrackSource? Source { get; init; }

    /// <summary>Whether this is an aux bus.</summary>
    public bool IsBus => BusSlot >= 0;
}
