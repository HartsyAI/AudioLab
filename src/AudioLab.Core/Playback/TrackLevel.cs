namespace AudioLab.Core.Playback;

/// <summary>The peak level of one track in the engine's mix.</summary>
/// <param name="Track">The track's id.</param>
/// <param name="Level">Its peaks after its fader and pan.</param>
public readonly record struct TrackLevel(string Track, LevelPair Level);
