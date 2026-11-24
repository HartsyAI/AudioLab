using System.Net;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Media;

/// <summary>What the user may upload as the server last said, and what this page has already promised against it.</summary>
/// <remarks>
/// The numbers are advice: another upload can finish first, and the server counts a running upload by what it has received. So a file is
/// refused only when the numbers say it cannot fit, a failed read says nothing (the server then judges the file), and a read that is slow is
/// waited for only a few seconds.
/// </remarks>
/// <param name="api">The media client; its upload ceiling follows what the server says, up to the page's own.</param>
/// <param name="clock">Times the wait for a slow answer.</param>
/// <param name="log">Where a read that failed is written.</param>
internal sealed class UploadLimits(MediaApi api, TimeProvider clock, ILogger log)
{
    /// <summary>How long a file waits for a read of the limits that is on its way before the server is left to judge it.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    private readonly Lock _gate = new();

    // Uploads of this page that passed the quota check and that the numbers read so far may not count yet.
    private readonly List<UploadJob> _promised = [];

    private Task? _reading;

    // The read that was not waited for the whole of: files do not wait for it again, it is as good as failed until it ends.
    private Task? _gaveUpOn;

    private bool _active;

    private bool _again;

    private bool _firstEnded;

    private int _started;

    private long? _served;

    /// <summary>The places for uploads that run at once: three, or fewer when the server lets one user run fewer.</summary>
    public UploadSlots Slots { get; } = new(MediaLibrary.ParallelUploads);

    /// <summary>The limits as the server last said; null until it has, or after a read that failed.</summary>
    public MediaLimits? Current { get; private set; }

    /// <summary>Reads the limits; a read that is on its way is read again after it, so a change that came meanwhile is in the answer.</summary>
    public Task ReadAsync()
    {
        lock (_gate)
        {
            if (_active)
            {
                _again = true;
                return _reading!;
            }
            _active = true;
            return _reading = KeepAsync();
        }
    }

    /// <summary>Waits, a few seconds at most, for the first read of all, so that the places for uploads are the server's number.</summary>
    public Task FirstAsync()
    {
        return Volatile.Read(ref _firstEnded) ? Task.CompletedTask : ReadyAsync(false);
    }

    /// <summary>Waits, a few seconds at most, for the read that is on its way, or for a new one when asked.</summary>
    /// <param name="fresh">Whether the numbers must be read after now: a file that was refused is sent again.</param>
    /// <returns>Whether the numbers are known and as new as that; false means the server is to judge the file.</returns>
    public async Task<bool> ReadyAsync(bool fresh)
    {
        Task read;
        lock (_gate)
        {
            read = fresh || _reading is null ? ReadAsync() : _reading;
        }
        if (!read.IsCompleted)
        {
            lock (_gate)
            {
                if (ReferenceEquals(read, _gaveUpOn))
                {
                    return false;
                }
            }
            using CancellationTokenSource stop = new();
            Task patience = Task.Delay(Patience, clock, stop.Token);
            if (await Task.WhenAny(read, patience) != read)
            {
                lock (_gate)
                {
                    _gaveUpOn = read;
                }
                return false;
            }
            await stop.CancelAsync();
        }
        lock (_gate)
        {
            return Current is not null;
        }
    }

    /// <summary>The sentence for a file over the size limit, or null; the page's own ceiling is named when it is the reason.</summary>
    public string? TooLarge(long length)
    {
        long most = api.MaxUploadBytes;
        if (length <= most)
        {
            return null;
        }
        long? served;
        lock (_gate)
        {
            served = _served;
        }
        return served is { } offered && offered > most
            ? UploadChecks.TooLargeForPage(length, most, offered)
            : UploadChecks.TooLarge(length, most);
    }

    /// <summary>Checks the file against what is left of the quota after what this page has promised, and promises it when it fits.</summary>
    /// <returns>The reason the quota has no room for it, or null when it fits.</returns>
    public string? Promise(UploadJob job)
    {
        lock (_gate)
        {
            if (Current is not { } limits)
            {
                return null;
            }
            string? refusal = UploadChecks.AgainstQuota(
                limits.MaxFiles,
                limits.FilesRemaining - _promised.Count,
                limits.BytesRemaining - _promised.Sum(promised => promised.Length),
                job.Length);
            if (refusal is null)
            {
                job.SentAfterRead = int.MaxValue;
                _promised.Add(job);
            }
            return refusal;
        }
    }

    /// <summary>Notes that the job's body is on its way: a read that begins from now on counts it, so its promise ends with that read.</summary>
    public void Sending(UploadJob job)
    {
        lock (_gate)
        {
            job.SentAfterRead = _started;
        }
    }

    /// <summary>Ends the job's promise: it is over, and the next read counts what it left.</summary>
    public void Release(UploadJob job)
    {
        lock (_gate)
        {
            _promised.Remove(job);
        }
    }

    private async Task KeepAsync()
    {
        bool again;
        do
        {
            int number;
            lock (_gate)
            {
                _again = false;
                number = ++_started;
            }
            try
            {
                Apply(await api.LimitsAsync(), number);
            }
            catch (OperationCanceledException)
            {
                // Not the user's: nothing here is cancelled by one. What was known is kept.
            }
            catch (Exception exception)
            {
                Forget();
                Log(exception);
            }
            lock (_gate)
            {
                again = _again;
                _active = again;
            }
        }
        while (again);
        Volatile.Write(ref _firstEnded, true);
    }

    private void Apply(MediaLimits limits, int number)
    {
        lock (_gate)
        {
            Current = limits;
            if (limits.MaxUploadBytes > 0)
            {
                _served = limits.MaxUploadBytes;
                api.MaxUploadBytes = Math.Min(limits.MaxUploadBytes, MediaApi.MemoryCeilingBytes);
            }
            _promised.RemoveAll(promised => promised.SentAfterRead < number);
        }
        // The page keeps one place free for another tab or window, and never takes more than the server lets one user run.
        Slots.SetLimit(Math.Clamp(limits.MaxConcurrentUploads - 1, 1, MediaLibrary.ParallelUploads));
    }

    // A read that failed says nothing about the quota, and a stale answer would go on refusing files that fit: the server judges them.
    // The size ceiling is kept: it changes rarely, and a file over it never fits.
    private void Forget()
    {
        lock (_gate)
        {
            Current = null;
            _promised.Clear();
        }
    }

    private void Log(Exception exception)
    {
        if (exception is ApiException { Status: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or >= HttpStatusCode.InternalServerError })
        {
            MediaLog.LimitsUnavailable(log, exception);
        }
        else
        {
            MediaLog.LimitsFailed(log, exception);
        }
    }
}
