namespace AudioLab.Server.Transcription;

/// <summary>The user already has as many queued or running jobs as the server allows (D95).</summary>
public sealed class TranscriptionLimitException(string message) : Exception(message);
