using AudioLab.Core.Model;

namespace AudioLab.Audio.Live;

/// <summary>Where the live engine gets a media file's audio from, decoded at the file's own rate.</summary>
public interface IClipSource
{
    /// <summary>Fetches and decodes a media file; fails when it cannot be fetched or decoded.</summary>
    /// <param name="id">The media file.</param>
    /// <param name="pause">Awaited between slices of work, so the thread that decodes stays responsive.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    public Task<PcmAudio> LoadAsync(MediaId id, Func<CancellationToken, Task> pause, CancellationToken cancellationToken);
}
