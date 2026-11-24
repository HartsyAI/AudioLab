namespace AudioLab.Web.Media;

/// <summary>One file on its way to the media store, as the library panel shows it.</summary>
public sealed class UploadJob
{
    private TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal UploadJob(long id, IUploadSource source)
    {
        Id = id;
        Source = source;
    }

    /// <summary>A number that is unique among the jobs of this page.</summary>
    public long Id { get; }

    /// <summary>The file's name.</summary>
    public string Name => Source.Name;

    /// <summary>The file's size in bytes.</summary>
    public long Length => Source.Length;

    /// <summary>Where the upload is.</summary>
    public UploadState State { get; internal set; } = UploadState.Queued;

    /// <summary>The share of the file handed to the browser, from 0 to 1.</summary>
    public double Fraction { get; internal set; }

    /// <summary>When the wait for the server's answer began; null before that.</summary>
    public DateTimeOffset? SendingSince { get; internal set; }

    /// <summary>Why the upload ended without a file, in one sentence fit for the user; null until it fails.</summary>
    public string? Failure { get; internal set; }

    /// <summary>Completes when the upload ends, whether it ended in a file, a refusal or a cancel; a retry starts a new one.</summary>
    public Task Ended => _ended.Task;

    internal IUploadSource Source { get; }

    // True once Try again has sent the file again, so the limits are read afresh before it is judged.
    internal bool Retried { get; set; }

    // Which read of the limits began after the body was on its way, so that read counts the upload; no read does until then.
    internal int SentAfterRead { get; set; } = int.MaxValue;

    internal void Restart() => _ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void End() => _ended.TrySetResult();

    internal CancellationTokenSource? Cancellation { get; set; }
}
