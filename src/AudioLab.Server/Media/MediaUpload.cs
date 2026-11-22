using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>One upload that is running: what it has received counts against the user's quota until it ends, so uploads cannot add up past it.</summary>
public sealed class MediaUpload : IDisposable
{
    private readonly MediaStore _store;
    private readonly UserId _user;
    private readonly UserMedia _media;
    private bool _disposed;

    internal MediaUpload(MediaStore store, UserId user, UserMedia media)
    {
        _store = store;
        _user = user;
        _media = media;
    }

    /// <summary>How many bytes the upload has received.</summary>
    public long Bytes { get; private set; }

    /// <summary>Refuses an upload that is announced to be <paramref name="incoming"/> bytes when the user has no room for it.</summary>
    /// <exception cref="MediaQuotaExceededException">The quota says no.</exception>
    public async ValueTask RequireRoomAsync(long incoming, CancellationToken cancellationToken)
    {
        if (await _store.UploadRefusalAsync(_user, _media, Bytes, incoming, cancellationToken) is { } refusal)
        {
            throw new MediaQuotaExceededException(refusal);
        }
    }

    /// <summary>Counts <paramref name="count"/> more bytes received and refuses when they take the user past the quota.</summary>
    /// <exception cref="MediaQuotaExceededException">The quota says no.</exception>
    public async ValueTask AddAsync(long count, CancellationToken cancellationToken)
    {
        lock (_media.Sync)
        {
            Bytes += count;
            _media.UploadBytes += count;
        }
        await RequireRoomAsync(Bytes, cancellationToken);
    }

    /// <summary>Ends the upload, whether or not it was stored, and gives back what it had taken.</summary>
    public void Dispose()
    {
        lock (_media.Sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _media.UploadsRunning--;
            _media.UploadBytes -= Bytes;
        }
    }
}
