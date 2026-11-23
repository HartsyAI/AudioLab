namespace AudioLab.Web.Waveforms;

/// <summary>What a clip draws for its waveform: nothing yet, a path from the file's peaks, or the reason there is none.</summary>
/// <param name="Phase">Where the waveform is.</param>
/// <param name="Path">The path in a 1000 by 100 box, to be stretched over the clip's body; set only when ready.</param>
/// <param name="Reason">Why the peaks could not be loaded, in one sentence fit for the user; set only when failed.</param>
public readonly record struct ClipWave(WavePhase Phase, string? Path = null, string? Reason = null)
{
    /// <summary>The state of a waveform whose peaks have not arrived.</summary>
    public static ClipWave Loading => new(WavePhase.Loading);
}
