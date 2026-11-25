using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Media;

namespace AudioLab.Server.Transcription;

/// <summary>What a user can do with the transcription of a media file. Every method takes the user from the door.</summary>
public sealed class TranscriptionService(MediaService media, TranscriptionJobs jobs, TranscriptionStore store)
{
    /// <summary>Starts a job for one of the user's media files, or returns the job already there.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    /// <exception cref="TranscriptionLimitException">The user has as many queued or running jobs as allowed.</exception>
    public Task<StartOutcome> StartAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        MediaRecord record = media.Get(user, id);
        return jobs.StartAsync(user, record, cancellationToken);
    }

    /// <summary>The status of the job of one of the user's media files.</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    /// <exception cref="TranscriptionNotFoundException">The file has no job.</exception>
    public async Task<TranscriptionStatus> StatusAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        media.Get(user, id);
        return await jobs.StatusAsync(user, id, cancellationToken)
            ?? throw new TranscriptionNotFoundException("There is no transcription of that file.");
    }

    /// <summary>Cancels the job of one of the user's media files (D93).</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    public Task<TranscriptionStatus> CancelAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        media.Get(user, id);
        return jobs.CancelAsync(user, id, cancellationToken);
    }

    /// <summary>Queues the job of one of the user's media files again (D93).</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    public Task<TranscriptionStatus> ResumeAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        media.Get(user, id);
        return jobs.ResumeAsync(user, id, cancellationToken);
    }

    /// <summary>The stored transcript of one of the user's media files (D94).</summary>
    /// <exception cref="MediaNotFoundException">The user has no such file.</exception>
    /// <exception cref="TranscriptionNotFoundException">The file has no job and no transcript.</exception>
    /// <exception cref="TranscriptionConflictException">The job is not done yet.</exception>
    /// <exception cref="TranscriptRefusedException">The stored transcript is not one the server accepts.</exception>
    public async Task<TranscriptDocument> TranscriptAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        MediaRecord record = media.Get(user, id);
        TranscriptDocument? document = await store.ReadTranscriptAsync(user, id, cancellationToken);
        if (document is null)
        {
            TranscriptionState state = await jobs.StateOfAsync(user, id, cancellationToken)
                ?? throw new TranscriptionNotFoundException("There is no transcription of that file.");
            throw state == TranscriptionState.Done
                ? new TranscriptionNotFoundException("The transcript of that file is missing; resume the transcription to make it again.")
                : new TranscriptionConflictException($"The transcription is {state.ToString().ToLowerInvariant()}; it has no transcript yet.");
        }
        if (document.Sha256 != record.Sha256)
        {
            throw new TranscriptRefusedException(["The stored transcript belongs to another version of the file."]);
        }
        return document;
    }
}
