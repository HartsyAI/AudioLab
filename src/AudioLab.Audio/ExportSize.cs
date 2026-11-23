namespace AudioLab.Audio;

/// <summary>How large an export will be, known before its first byte.</summary>
/// <param name="Bytes">The size of the whole file: a WAV file, or a ZIP archive of WAV files.</param>
/// <param name="Frames">The frames of the longest WAV file in it.</param>
/// <param name="Files">How many WAV files it holds.</param>
public readonly record struct ExportSize(long Bytes, long Frames, int Files);
