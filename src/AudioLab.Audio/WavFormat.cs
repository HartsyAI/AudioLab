namespace AudioLab.Audio;

/// <summary>How the samples of a WAV file are stored.</summary>
public enum WavFormat
{
    /// <summary>16-bit integer PCM.</summary>
    Pcm16,

    /// <summary>24-bit integer PCM.</summary>
    Pcm24,

    /// <summary>32-bit IEEE float, which keeps every sample as rendered, even past full scale.</summary>
    Float32,
}
