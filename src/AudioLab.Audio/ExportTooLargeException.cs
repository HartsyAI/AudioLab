namespace AudioLab.Audio;

/// <summary>An export would be larger than its file format can hold; the message says which limit.</summary>
public sealed class ExportTooLargeException(string message) : Exception(message);
