namespace AudioLab.Server.Render;

/// <summary>What a render of a project may use, under <c>AudioLab:Render</c>.</summary>
public sealed record RenderOptions
{
    /// <summary>The most bytes of decoded float samples one media file may take while a render reads it; the default is 1 GiB.</summary>
    /// <remarks>A 16-bit upload at the default upload limit decodes to twice its size, so this is the most an upload may need.</remarks>
    public long MaxDecodedBytesPerFile { get; init; } = 1024L * 1024 * 1024;

    /// <summary>The most bytes of cleaned, resampled media one render keeps at once; the file in use is always kept; 512 MiB by default.</summary>
    public long MediaCacheBytes { get; init; } = 512L * 1024 * 1024;

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
