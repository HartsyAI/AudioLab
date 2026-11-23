using AudioLab.Core.Model;

namespace AudioLab.Web.Waveforms;

/// <summary>What the store knows of a media file's peaks at one zoom.</summary>
/// <param name="Phase">Whether the level is here, on its way, or not to be had.</param>
/// <param name="Header">The file's header, when the level is here.</param>
/// <param name="Level">The level the zoom needs, when it is here.</param>
/// <param name="Failure">Why there are no peaks, when they failed.</param>
public sealed record PeakView(WavePhase Phase, PeakHeader? Header, PeakLevel? Level, string? Failure);
