using AudioLab.Audio;
using AudioLab.Core.Model;
using AudioLab.Server.Media;

namespace AudioLab.Server.Render;

/// <summary>Makes the media of a render: a source that reads one user's files, and a cache of its own for that one render.</summary>
public sealed class RenderMediaProvider(MediaStore store, RenderOptions options, ILogger<RenderMediaProvider> logger)
{
    /// <summary>Reads the user's media folder if that has not been done and returns the media of one render at <paramref name="sampleRate"/>.</summary>
    /// <remarks>
    /// The cache is the render's own: the engine's cache answers from memory by media id without asking its source, so one shared between
    /// renders would hand a user the audio another user's render had decoded, and would keep a file after it was deleted.
    /// </remarks>
    /// <exception cref="IOException">The user's media folder cannot be read.</exception>
    public async Task<RenderMedia> OpenAsync(UserId user, int sampleRate, CancellationToken cancellationToken)
    {
        // Off the request's thread, and so that an unreadable folder is a failure and not a file that is missing.
        await store.PrepareAsync(user, cancellationToken);
        store.RequireReadable(user);
        UserMediaSource source = new(store, user, options.MaxDecodedBytesPerFile, logger, cancellationToken);
        return new RenderMedia(source, new MediaCache(source, sampleRate, options.MediaCacheBytes));
    }
}
