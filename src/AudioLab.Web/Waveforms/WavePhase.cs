namespace AudioLab.Web.Waveforms;

/// <summary>Where an audio clip's waveform is: not here yet, drawn from the file's peaks, or not to be had.</summary>
public enum WavePhase
{
    /// <summary>The peaks are on their way; nothing is drawn yet.</summary>
    Loading,

    /// <summary>The path is drawn from the media file's own peaks.</summary>
    Ready,

    /// <summary>The peaks could not be loaded; <see cref="ClipWave.Reason"/> says why.</summary>
    Failed,
}
