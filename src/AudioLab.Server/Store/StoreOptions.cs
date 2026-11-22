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

    /// <summary>The largest project file the store reads or writes, in bytes.</summary>
    public long MaxFileBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>The root folder as an absolute path.</summary>
    public string ResolveRoot()
    {
        string root = Root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioLab", "store");
        return Path.IsPathFullyQualified(root)
            ? Path.GetFullPath(root)
            : throw new InvalidOperationException("The store root must be an absolute path.");
    }
}
