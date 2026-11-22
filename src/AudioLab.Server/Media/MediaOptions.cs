namespace AudioLab.Server.Media;

/// <summary>What the media store accepts and keeps, under <c>AudioLab:Media</c>.</summary>
public sealed record MediaOptions
{
    /// <summary>The most bytes one upload may have; the default is 512 MiB, about 25 minutes of 48 kHz stereo 32-bit audio.</summary>
    public long MaxUploadBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>The most media files one user may keep.</summary>
    public int MaxFilesPerUser { get; init; } = 5000;

    /// <summary>The most bytes of media one user may keep; the default is 20 GiB.</summary>
    public long MaxBytesPerUser { get; init; } = 20L * 1024 * 1024 * 1024;
}
