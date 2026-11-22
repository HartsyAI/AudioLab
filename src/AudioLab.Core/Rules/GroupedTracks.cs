using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The track list and the groups after a rule has put them in order.</summary>
/// <param name="Tracks">The tracks, each group's members together.</param>
/// <param name="Groups">The groups that still have members.</param>
public sealed record GroupedTracks(IReadOnlyList<Track> Tracks, IReadOnlyList<TrackGroup> Groups);
