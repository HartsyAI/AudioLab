using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Where a clip is to go: an absolute start, and the track when it changes, as <c>clip_move</c> takes them (D22).</summary>
/// <param name="Clip">The clip to move.</param>
/// <param name="Start">Where it starts, in seconds; an absolute time, so a drag that came back lands exactly where it began.</param>
/// <param name="Track">The track it goes to, or null to stay on its own.</param>
public readonly record struct ClipMove(ClipId Clip, double Start, TrackId? Track);
