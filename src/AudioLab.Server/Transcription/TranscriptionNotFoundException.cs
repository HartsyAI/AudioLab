namespace AudioLab.Server.Transcription;

/// <summary>The media file has no transcription job, or no transcript, that the user can see.</summary>
public sealed class TranscriptionNotFoundException(string message) : Exception(message);
