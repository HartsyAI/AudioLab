namespace AudioLab.Web.Media;

/// <summary>One file on its way to the media store, as the library panel shows it.</summary>
public sealed class UploadJob
{
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

    internal IUploadSource Source { get; }

    internal CancellationTokenSource? Cancellation { get; set; }
}
