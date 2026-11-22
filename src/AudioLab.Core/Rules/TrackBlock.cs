using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>A unit that moves as a whole in the track list: one ungrouped track, or a group with all its members.</summary>
/// <param name="Group">The group, or null for a single ungrouped track.</param>
/// <param name="Tracks">The tracks of the block, in list order.</param>
public sealed record TrackBlock(GroupId? Group, IReadOnlyList<Track> Tracks);
