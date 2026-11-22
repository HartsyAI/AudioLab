namespace AudioLab.Server.Store;

/// <summary>The store folder, held by this process alone: creates it, locks it and removes what an interrupted write left in it.</summary>
public sealed class StoreRoot : IDisposable
{
    private readonly FileStream _lock;

    /// <summary>Takes the store at the configured root.</summary>
    /// <exception cref="StoreLockedException">Another process holds the store.</exception>
    public StoreRoot(StoreOptions options, AtomicFile atomic, ILogger<StoreRoot> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        Atomic = atomic;
        Paths = new StorePaths(options.ResolveRoot());
        Directory.CreateDirectory(Paths.Root);
        _lock = TakeLock(Paths);
        StoreLog.TempSwept(logger, Sweep(Paths, logger));
    }

    /// <summary>The options the store runs with.</summary>
    public StoreOptions Options { get; }

    /// <summary>The paths of everything in the store.</summary>
    public StorePaths Paths { get; }

    /// <summary>The writer every store file goes through.</summary>
    public AtomicFile Atomic { get; }

    /// <summary>Releases the store to other processes.</summary>
    public void Dispose() => _lock.Dispose();

    private static FileStream TakeLock(StorePaths paths)
    {
        try
        {
            return new FileStream(paths.LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException exception)
        {
            throw new StoreLockedException(paths.Root, exception);
        }
    }

    // Safe only because the lock makes this process the one writer: no write of ours is in flight at construction.
    private static int Sweep(StorePaths paths, ILogger logger)
    {
        if (!Directory.Exists(paths.UsersDirectory))
        {
            return 0;
        }
        int removed = 0;
        foreach (string file in Directory.EnumerateFiles(paths.UsersDirectory, "*.tmp", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (!StorePaths.IsLeftoverTemp(name))
            {
                continue;
            }
            try
            {
                File.Delete(file);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                StoreLog.SweepFailed(logger, name, exception.GetType().Name);
            }
        }
        return removed;
    }
}
