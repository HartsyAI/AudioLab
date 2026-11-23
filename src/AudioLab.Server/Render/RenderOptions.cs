namespace AudioLab.Server.Render;

/// <summary>What a render of a project may use, under <c>AudioLab:Render</c>.</summary>
public sealed record RenderOptions
{
    /// <summary>The most bytes one media file may take as float samples, as it is and at the render's rate; 1 GiB by default.</summary>
    /// <remarks>
    /// Only the first two channels are kept, as the engine uses no more. Both sizes are worked out from the header before anything is
    /// allocated, so a file that would pass at its own rate and not at the render's is refused as soon as the rate is known. The file
    /// is held in both forms while it is converted, so one file takes at most twice this.
    /// </remarks>
    public long MaxDecodedBytesPerFile { get; init; } = 1024L * 1024 * 1024;

    /// <summary>The most bytes of media one render holds at the render's rate, every distinct file counted once; 2 GiB by default.</summary>
    /// <remarks>A render that needs more is refused with a limit: it never decodes a file twice and never evicts one.</remarks>
    public long MaxDecodedBytesPerRender { get; init; } = 2L * 1024 * 1024 * 1024;

    /// <summary>The most exports one user may have running at once; one more is refused with 429.</summary>
    public int MaxConcurrentExportsPerUser { get; init; } = 2;

    /// <summary>The most exports the server runs at once, whoever asks; one more is refused with 429. An export keeps a core busy.</summary>
    public int MaxConcurrentExports { get; init; } = Math.Max(2, Environment.ProcessorCount / 2);

    /// <summary>The longest audio one file of an export may hold, in seconds; the default is one hour.</summary>
    public double MaxSeconds { get; init; } = 3600;

    /// <summary>The most bytes one export may send, a WAV file or a ZIP archive; 2 GiB by default, and 4 GiB is what the formats hold.</summary>
    public long MaxBytes { get; init; } = 2L * 1024 * 1024 * 1024;

    /// <summary>The longest one export may run, from the request until its last byte; a longer one is stopped. The default is 10 minutes.</summary>
    public TimeSpan MaxTime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>The longest tail a request may ask for, in seconds.</summary>
    public double MaxTailSeconds { get; init; } = 60;
}
