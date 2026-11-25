using AudioLab.Core.Model;

namespace AudioLab.Web.Screens;

/// <summary>One thing the mixer draws in its row: a group's strip, with how many tracks it holds, or a track's strip.</summary>
/// <param name="Group">The group, when this is its strip; otherwise null.</param>
/// <param name="Track">The track, when this is a track's strip; otherwise null.</param>
/// <param name="Members">The number of tracks in the group, for its caption.</param>
public readonly record struct MixerLane(TrackGroup? Group, Track? Track, int Members);
