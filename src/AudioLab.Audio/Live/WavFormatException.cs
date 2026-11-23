namespace AudioLab.Audio.Live;

/// <summary>A WAV file that <see cref="WavDecoder"/> cannot decode; the message says why.</summary>
public sealed class WavFormatException(string reason) : Exception(reason);
