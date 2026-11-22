namespace AudioLab.Server.Store;

/// <summary>Another process already uses the store folder.</summary>
public sealed class StoreLockedException : Exception
{
    /// <summary>Creates the exception for the store at <paramref name="root"/>.</summary>
    public StoreLockedException(string root, Exception? inner = null)
        : base($"Another AudioLab server is using the store at {root}.", inner)
    {
    }
}
