using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>One upload that is running: what it has received counts against the user's quota until it ends, so uploads cannot add up past it.</summary>
public sealed class MediaUpload : IDisposable
{
    private readonly MediaStore _store;
    private readonly UserId _user;
    private readonly UserMedia _media;
    private bool _disposed;
    private bool _admitted;

    internal MediaUpload(MediaStore store, UserId user, UserMedia media)
    {
        _store = store;
        _user = user;
        _media = media;
    }

    /// <summary>How many bytes the upload has received.</summary>
    public long Bytes { get; private set; }

    /// <summary>Asks the quota to admit the upload, announced to be <paramref name="incoming"/> bytes; admitted uploads are counted against each other.</summary>
    /// <exception cref="MediaQuotaExceededException">The quota says no.</exception>
    public async ValueTask AdmitAsync(long incoming, CancellationToken cancellationToken)
    {
        await _media.Admission.WaitAsync(cancellationToken);
        try
        {
            if (await _store.UploadRefusalAsync(_user, _media, false, 0, incoming, cancellationToken) is { } refusal)
            {
                throw new MediaQuotaExceededException(refusal);
            }
            lock (_media.Sync)
            {
                _admitted = true;
                _media.UploadsAdmitted++;
            }
        }
        finally
        {
            _media.Admission.Release();
        }
    }

    /// <summary>Counts <paramref name="count"/> more bytes received and refuses when they take the user past the quota.</summary>
    /// <exception cref="MediaQuotaExceededException">The quota says no.</exception>
    /// <exception cref="InvalidOperationException">The upload was not admitted first.</exception>
    public async ValueTask AddAsync(long count, CancellationToken cancellationToken)
    {
        lock (_media.Sync)
        {
            if (!_admitted)
            {
                throw new InvalidOperationException("An upload must be admitted before it receives bytes.");
            }
            Bytes += count;
            _media.UploadBytes += count;
        }
        if (await _store.UploadRefusalAsync(_user, _media, true, Bytes, Bytes, cancellationToken) is { } refusal)
        {
            throw new MediaQuotaExceededException(refusal);
        }
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
            if (_admitted)
            {
                _media.UploadsAdmitted--;
                _media.UploadBytes -= Bytes;
            }
        }
    }
}
