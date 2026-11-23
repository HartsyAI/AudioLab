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
    private const int ParallelUploads = 3;

    private readonly SemaphoreSlim _slots = new(ParallelUploads);

    // The page runs on one thread; the lock keeps the lists whole for a host that does not.
    private readonly Lock _gate = new();

    private readonly List<UploadJob> _uploads = [];

    // Files that finished uploading before the first read of the list came back, which that read may not include.
    private readonly List<MediaSummary> _addedBeforeReady = [];

    private IReadOnlyList<MediaSummary> _files = [];

    private long _next;

    private Task? _loading;

    /// <summary>Raised when the list, the uploads or the state changed.</summary>
    public event Action? Changed;

    /// <summary>Whether the list has been read.</summary>
    public LibraryState State { get; private set; }

    /// <summary>Why the list could not be read, when <see cref="State"/> is failed.</summary>
    public string? Failure { get; private set; }

    /// <summary>The files, newest first.</summary>
    public IReadOnlyList<MediaSummary> Files => _files;

    /// <summary>How many stored records the server left out as invalid.</summary>
    public int Refused { get; private set; }

    /// <summary>The uploads that are waiting, running or refused, oldest first.</summary>
    public IReadOnlyList<UploadJob> Uploads => _uploads;

    /// <summary>The file with this id, or null.</summary>
    public MediaSummary? Find(string id) => _files.FirstOrDefault(file => file.Id == id);

    /// <summary>Reads the list from the server; a read that is already running is the one awaited.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return _loading is { IsCompleted: false } running ? running : _loading = ReadAsync(cancellationToken);
    }

    /// <summary>Starts uploading the chosen files, a few at a time, and returns their jobs at once; each file's outcome shows in <see cref="Uploads"/> and in a toast.</summary>
    public IReadOnlyList<UploadJob> Add(IEnumerable<IUploadSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        List<UploadJob> added = [];
        foreach (IUploadSource source in sources)
        {
            UploadJob job;
            lock (_gate)
            {
                job = new UploadJob(++_next, source);
                _uploads.Add(job);
            }
            added.Add(job);
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
            Changed?.Invoke();
            _ = RunAsync(failed);
        }
    }

    /// <summary>Stops an upload that is waiting or running, or removes a refused one from the list; the server keeps nothing of a stopped upload.</summary>
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
            MediaListing listing = await api.ListAsync(cancellationToken);
            HashSet<string> listed = [.. listing.Files.Select(file => file.Id)];
            lock (_gate)
            {
                _files = [.. _addedBeforeReady.Where(file => !listed.Contains(file.Id)).Reverse(), .. listing.Files];
                _addedBeforeReady.Clear();
            }
            Refused = listing.Refused;
            State = LibraryState.Ready;
        }
        catch (OperationCanceledException)
        {
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

    private async Task RunAsync(UploadJob job)
    {
        using CancellationTokenSource cancellation = new();
        job.Cancellation = cancellation;
        try
        {
            await _slots.WaitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Remove(job);
            return;
        }
        try
        {
            job.State = UploadState.Reading;
            job.Fraction = 0;
            Changed?.Invoke();
            string? problem = await CheckAsync(job.Source);
            if (problem is not null)
            {
                Fail(job, problem);
                return;
            }
            MediaSummary created = await SendAsync(job, cancellation.Token);
            lock (_gate)
            {
                _files = [created, .. _files.Where(file => file.Id != created.Id)];
                if (State != LibraryState.Ready)
                {
                    _addedBeforeReady.Add(created);
                }
            }
            Remove(job);
        }
        catch (OperationCanceledException)
        {
            Remove(job);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MediaLog.UploadFailed(log, exception);
            Fail(job, FailureText.Of(exception));
        }
        finally
        {
            job.Cancellation = null;
            _slots.Release();
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
            }
            Changed?.Invoke();
        });
        return await api.UploadAsync(job.Name, job.Source.Open(), job.Length, progress, cancellationToken);
    }

    private static async Task<string?> CheckAsync(IUploadSource source)
    {
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
        toasts.Show(ToastKind.Error, $"{job.Name} was not added. {reason}");
        Changed?.Invoke();
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
