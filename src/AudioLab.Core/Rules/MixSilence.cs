namespace AudioLab.Core.Rules;

/// <summary>Why a track is silent in the mix; <see cref="None"/> when it is heard.</summary>
public enum MixSilence
{
    /// <summary>The track is heard.</summary>
    None,

    /// <summary>The track's own mute is on.</summary>
    TrackMuted,

    /// <summary>The track's group is muted.</summary>
    GroupMuted,

    /// <summary>Another track or group is soloed, and this one is not.</summary>
    SoloElsewhere,
}
