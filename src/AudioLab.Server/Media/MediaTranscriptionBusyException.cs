namespace AudioLab.Server.Media;

/// <summary>A media file cannot be deleted while a transcription of it is queued or running.</summary>
public sealed class MediaTranscriptionBusyException(string message) : Exception(message);
