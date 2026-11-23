using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>One stem of a project, rendered.</summary>
/// <param name="Track">The track, or aux bus, the stem is of.</param>
/// <param name="Name">The track's name.</param>
/// <param name="Audio">The stem; every stem of one export is as long as the longest.</param>
public sealed record Stem(TrackId Track, string Name, RenderedAudio Audio);
