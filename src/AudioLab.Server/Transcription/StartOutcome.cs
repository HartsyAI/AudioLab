using AudioLab.Core.Transcription;

namespace AudioLab.Server.Transcription;

/// <summary>The answer to a start: the job's status, and whether this start made the job.</summary>
/// <param name="Status">The job's status.</param>
/// <param name="Created">True when this start made the job; false when one was already there.</param>
public readonly record struct StartOutcome(TranscriptionStatus Status, bool Created);
