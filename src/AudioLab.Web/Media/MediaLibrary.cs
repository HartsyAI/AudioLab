using System.Net;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Services;

namespace AudioLab.Web.Media;

/// <summary>The user's media files as the page shows them: the list, the uploads on their way, and what refuses either.</summary>
/// <remarks>It outlives the panel that shows it, so a drop while another tab is open still uploads and still reports.</remarks>
public sealed class MediaLibrary(MediaApi api, ToastService toasts, TimeProvider clock, ILogger<MediaLibrary> log)
{
    // The server takes four uploads of a user at once (D37); the page keeps one free for another tab or window.
    internal const int ParallelUploads = 3;

    private readonly UploadLimits _limits = new(api, clock, log);

    // The page runs on one thread; the lock keeps the lists whole for a host that does not.
    private readonly Lock _gate = new();

    private readonly List<UploadJob> _uploads = [];

    // Files that finished uploading before the first read of the list came back, which that read may not include.
    private readonly List<MediaSummary> _addedBeforeReady = [];

    private IReadOnlyList<MediaSummary> _files = [];

    private long _next;

    private Task? _loading;

    // Counts what changed the files on the server, so a read that began before a change is never taken for the list after it.
    private int _version;

    private bool _refreshing;

    private Task? _refresh;

    private bool _refreshAgain;

    private readonly List<(string Name, string Reason)> _failed = [];

    private readonly List<string> _added = [];

    private int _sameName;

    /// <summary>Raised when the list, the uploads or the state changed.</summary>
    public event Action? Changed;

    /// <summary>Whether the list has been read.</summary>
    public LibraryState State { get; private set; }

    /// <summary>Why the list could not be read, when <see cref="State"/> is failed.</summary>
    public string? Failure { get; private set; }

    /// <summary>The files, newest first.</summary>
    public IReadOnlyList<MediaSummary> Files => _files;

    /// <summary>What the user may upload and what they have used, as the server last said; null until it has, or when it cannot say.</summary>
    public MediaLimits? Limits => _limits.Current;


    /// <summary>How many stored records the server left out as invalid.</summary>
    public int Refused { get; private set; }

    /// <summary>The uploads that are waiting, running or refused, oldest first.</summary>
    public IReadOnlyList<UploadJob> Uploads => _uploads;

    /// <summary>The file with this id, or null.</summary>
    public MediaSummary? Find(string id) => _files.FirstOrDefault(file => file.Id == id);

    /// <summary>Reads the list and the user's limits from the server; a list read that is already running is the one awaited.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Task limits = _limits.ReadAsync();
        Task list = _loading is { IsCompleted: false } running ? running : _loading = ReadAsync(cancellationToken);
        return Task.WhenAll(limits, list);
    }

    /// <summary>Starts uploading the chosen files, a few at a time, and returns their jobs; each outcome shows in <see cref="Uploads"/>.</summary>
    public IReadOnlyList<UploadJob> Add(IEnumerable<IUploadSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        List<UploadJob> added = [];
        HashSet<string> names = [.. _files.Select(file => file.Name)];
        foreach (IUploadSource source in sources)
        {
            if (!names.Add(source.Name))
            {
                _sameName++;
            }
            UploadJob job;
            lock (_gate)
            {
                job = new UploadJob(++_next, source);
                _uploads.Add(job);
            }
            added.Add(job);
        }
        // Every job of the batch is listed before the first one starts, so a job that ends at once does not end the batch.
        foreach (UploadJob job in added)
        {
            _ = RunAsync(job);
        }
        Changed?.Invoke();
        return added;
    }

    /// <summary>Sends a refused upload again.</summary>
    public void Retry(long job)
    {
        UploadJob? failed = _uploads.Find(candidate => candidate.Id == job);
        if (failed is { State: UploadState.Failed })
        {
            failed.State = UploadState.Queued;
            failed.Failure = null;
            failed.Fraction = 0;
            failed.SendingSince = null;
            failed.Retried = true;
            failed.Restart();
            Changed?.Invoke();
            _ = RunAsync(failed);
        }
    }

    /// <summary>Stops an upload that is waiting or running, or removes a refused one; the server keeps nothing of a stopped upload.</summary>
    public void Cancel(long job)
    {
        UploadJob? target = _uploads.Find(candidate => candidate.Id == job);
        if (target is null)
        {
            return;
        }
        target.Cancellation?.Cancel();
        if (target.State == UploadState.Failed)
        {
            Remove(target);
        }
    }

    /// <summary>Deletes a file. The server refuses while a project still plays it; the exception says which project.</summary>
    public async Task DeleteAsync(MediaSummary file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        try
        {
            await api.DeleteAsync(file.Id, cancellationToken);
        }
        catch (ApiException gone) when (gone.Status == HttpStatusCode.NotFound)
        {
            // Another window deleted it first: the list was stale, and the outcome is the one asked for.
        }
        lock (_gate)
        {
            _files = [.. _files.Where(candidate => candidate.Id != file.Id)];
            _version++;
        }
        Changed?.Invoke();
        _ = RefreshAsync();
    }

    /// <summary>Reads the list and the limits again without a load shown, after an upload or delete; a failure keeps the list.</summary>
    /// <remarks>Every call reads the limits: one that comes while a read is on its way is read once more after it.</remarks>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (State != LibraryState.Ready)
        {
            return LoadAsync(cancellationToken);
        }
        Task limits = _limits.ReadAsync();
        if (_refreshing)
        {
            _refreshAgain = true;
            return Task.WhenAll(limits, _refresh!);
        }
        return Task.WhenAll(limits, _refresh = KeepFreshAsync(cancellationToken));
    }

    private async Task KeepFreshAsync(CancellationToken cancellationToken)
    {
        _refreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                try
                {
                    await ApplyAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    // The list on the screen is still right as far as the page knows; the reason is for the console, not for the user.
                    MediaLog.ListFailed(log, exception);
                }
            }
            while (_refreshAgain && !cancellationToken.IsCancellationRequested);
        }
        finally
        {
            _refreshing = false;
        }
        Changed?.Invoke();
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        State = LibraryState.Loading;
        Failure = null;
        Changed?.Invoke();
        try
        {
            await ApplyAsync(cancellationToken);
            State = LibraryState.Ready;
        }
        catch (OperationCanceledException)
        {
            // Only the caller stops a read: the panel went, and the next one to open it reads again.
            State = LibraryState.NotLoaded;
        }
        catch (Exception exception)
        {
            MediaLog.ListFailed(log, exception);
            Failure = FailureText.Of(exception);
            State = LibraryState.Failed;
        }
        Changed?.Invoke();
    }

    // A read that began before a change on the server is read again, so a stale answer never undoes a delete or hides an upload.
    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        MediaListing listing;
        int version;
        int attempts = 0;
        do
        {
            version = _version;
            listing = await api.ListAsync(cancellationToken);
        }
        while (version != _version && ++attempts < 3);
        HashSet<string> listed = [.. listing.Files.Select(file => file.Id)];
        lock (_gate)
        {
            _files = [.. _addedBeforeReady.Where(file => !listed.Contains(file.Id)).Reverse(), .. listing.Files];
            _addedBeforeReady.Clear();
        }
        Refused = listing.Refused;
        Changed?.Invoke();
    }

    private async Task RunAsync(UploadJob job)
    {
        using CancellationTokenSource cancellation = new();
        job.Cancellation = cancellation;
        // Before the first place is taken, so that the number of places is the server's; a few seconds at most.
        await _limits.FirstAsync();
        try
        {
            await _limits.Slots.AcquireAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Remove(job);
            Settle();
            job.End();
            return;
        }
        try
        {
            job.State = UploadState.Reading;
            job.Fraction = 0;
            Changed?.Invoke();
            bool known = await _limits.ReadyAsync(job.Retried);
            string? problem = await CheckAsync(job, known);
            if (problem is not null)
            {
                Fail(job, problem);
                return;
            }
            MediaSummary created = await SendAsync(job, cancellation.Token);
            lock (_gate)
            {
                _version++;
                _files = [created, .. _files.Where(file => file.Id != created.Id)];
                if (State != LibraryState.Ready)
                {
                    _addedBeforeReady.Add(created);
                }
            }
            _added.Add(created.Name);
            Remove(job);
            _ = RefreshAsync();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user stopped it; any other cancellation is a failure to report, and the client turns those into other exceptions.
            Remove(job);
        }
        catch (Exception exception)
        {
            MediaLog.UploadFailed(log, exception);
            Fail(job, FailureText.Of(exception));
        }
        finally
        {
            _limits.Release(job);
            job.Cancellation = null;
            _limits.Slots.Release();
            Settle();
            job.End();
        }
    }

    private async Task<MediaSummary> SendAsync(UploadJob job, CancellationToken cancellationToken)
    {
        InlineProgress progress = new(report =>
        {
            job.Fraction = report.Fraction;
            if (report.BytesRead >= report.TotalBytes && job.State == UploadState.Reading)
            {
                job.State = UploadState.Sending;
                job.SendingSince = clock.GetUtcNow();
                _limits.Sending(job);
            }
            Changed?.Invoke();
        });
        return await api.UploadAsync(job.Name, job.Source.Open(), job.Length, progress, cancellationToken);
    }

    // A file is judged on limits that are as new as the last upload, delete or Try again; `known` is false when they could not be had in time,
    // and the server then judges the quota.
    private async Task<string?> CheckAsync(UploadJob job, bool known)
    {
        IUploadSource source = job.Source;
        if (_limits.TooLarge(source.Length) is { } tooLarge)
        {
            return tooLarge;
        }
        if (known && _limits.Promise(job) is { } full)
        {
            return full;
        }
        IReadOnlyList<string> name = MediaValidator.ValidateName(source.Name);
        if (name.Count > 0)
        {
            return $"The name is not valid: {name[0]}";
        }
        byte[] head = new byte[WavPrecheck.HeadBytes];
        int read = 0;
        await using (Stream stream = source.Open())
        {
            while (read < head.Length)
            {
                int count = await stream.ReadAsync(head.AsMemory(read));
                if (count == 0)
                {
                    break;
                }
                read += count;
            }
        }
        return WavPrecheck.Problem(head.AsSpan(0, read), source.Length);
    }

    private void Fail(UploadJob job, string reason)
    {
        job.State = UploadState.Failed;
        job.Failure = reason;
        _failed.Add((job.Name, reason));
        Changed?.Invoke();
    }

    // When nothing is waiting or running, the files added and the files not added are each said once, not one toast for each file.
    private void Settle()
    {
        if (_uploads.Exists(job => job.State != UploadState.Failed))
        {
            return;
        }
        if (_added.Count > 0)
        {
            string same = _sameName switch
            {
                0 => string.Empty,
                1 => " One has the name of a file you already had.",
                _ => $" {_sameName} have the names of files you already had.",
            };
            string text = _added.Count == 1 ? $"{_added[0]} was added." : $"{_added.Count} files were added.";
            toasts.Show(ToastKind.Success, text + same);
        }
        if (_failed.Count > 0)
        {
            (string name, string reason) = _failed[0];
            string text = _failed.Count == 1
                ? $"{name} was not added. {reason}"
                : $"{_failed.Count} files were not added; they are listed in the Media tab. The first, {name}: {reason}";
            toasts.Show(ToastKind.Error, text);
        }
        _added.Clear();
        _failed.Clear();
        _sameName = 0;
    }

    private void Remove(UploadJob job)
    {
        lock (_gate)
        {
            _uploads.Remove(job);
        }
        Changed?.Invoke();
    }

    private sealed class InlineProgress(Action<UploadProgress> report) : IProgress<UploadProgress>
    {
        public void Report(UploadProgress value) => report(value);
    }
}
