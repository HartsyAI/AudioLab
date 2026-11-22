namespace AudioLab.Server.Store;

/// <summary>Where the store keeps its files and how it autosaves.</summary>
public sealed record StoreOptions
{
    /// <summary>The folder the store keeps everything in; the per-user application data folder when not set. It must be an absolute path.</summary>
    public string? Root { get; init; }

    /// <summary>How long after the last change an open project is written.</summary>
    public TimeSpan AutosaveDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>The longest a changed project waits for its write, however steadily it keeps changing.</summary>
    public TimeSpan AutosaveMaxDelay { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>The largest project file the store reads or writes, in bytes; the default is 32 MiB and the hard limit is <see cref="MaxFileBytesLimit"/>.</summary>
    /// <remarks>
    /// A note serialises to about 80 bytes, so 32 MiB holds around 400,000 notes besides tracks, clips and lyrics, far
    /// more than a session needs; <c>ProjectLimits</c> allows a million items a list, which a file would never reach in use.
    /// A project is held in memory as a whole document, several times its file size, so the limit also bounds that.
    /// </remarks>
    public long MaxFileBytes { get; init; } = 32L * 1024 * 1024;

    /// <summary>The most projects one user may have; creating or duplicating one beyond it is refused.</summary>
    public int MaxProjectsPerUser { get; init; } = 1000;

    /// <summary>The highest value <see cref="MaxFileBytes"/> may be set to: 512 MiB, well under the 2 GiB a byte array cannot pass.</summary>
    public const long MaxFileBytesLimit = 512L * 1024 * 1024;

    /// <summary>The root folder as an absolute path.</summary>
    public string ResolveRoot()
    {
        string root = Root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioLab", "store");
        return Path.IsPathFullyQualified(root)
            ? Path.GetFullPath(root)
            : throw new InvalidOperationException("The store root must be an absolute path.");
    }
}
