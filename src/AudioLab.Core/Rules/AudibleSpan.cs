using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>A stretch of the timeline where one audio clip is heard, in seconds, with whether each end is a seam another clip made.</summary>
/// <param name="Clip">The clip that plays.</param>
/// <param name="Start">Where the stretch begins on the timeline.</param>
/// <param name="End">Where the stretch ends on the timeline.</param>
/// <param name="SeamIn">Whether the start is cut by a covering clip, not the clip's own start.</param>
/// <param name="SeamOut">Whether the end is cut by a covering clip, not the clip's own end.</param>
public readonly record struct AudibleSpan(ClipId Clip, double Start, double End, bool SeamIn, bool SeamOut);
