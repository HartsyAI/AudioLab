using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>What another feature holds about a media file that a delete must respect, such as a transcription (D94).</summary>
public interface IMediaRemoval
{
    /// <summary>Refuses the delete while the feature is using the file.</summary>
    /// <exception cref="MediaTranscriptionBusyException">The feature is using the file.</exception>
    public Task EnsureRemovableAsync(UserId user, MediaId id, CancellationToken cancellationToken);

    /// <summary>Removes what the feature keeps about the file, once the file is gone.</summary>
    public Task RemoveAsync(UserId user, MediaId id, CancellationToken cancellationToken);
}
