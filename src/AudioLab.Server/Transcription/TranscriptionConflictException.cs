namespace AudioLab.Server.Transcription;

/// <summary>The job is in a state the request does not allow, such as a resume of a finished job.</summary>
public sealed class TranscriptionConflictException(string message) : Exception(message);
