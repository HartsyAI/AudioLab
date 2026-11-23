namespace AudioLab.Audio.Wav;

/// <summary>A file that is not a WAV file the engine reads; the message says why and is safe to show a client.</summary>
public sealed class WavFormatException(string reason) : Exception(reason);
