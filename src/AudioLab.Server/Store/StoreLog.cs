namespace AudioLab.Server.Store;

/// <summary>The log messages of the store. File names are the store's own hashed names, never an id or a user's text.</summary>
internal static partial class StoreLog
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Warning, Message = "A temporary file could not be removed; the next start removes it.")]
    public static partial void TempNotRemoved(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Removed {Count} temporary files an interrupted write left behind.")]
    public static partial void TempSwept(ILogger logger, int count);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "The store refused the file {File}: {Problems}")]
    public static partial void FileRefused(ILogger logger, string file, string problems);

    [LoggerMessage(EventId = 103, Level = LogLevel.Error, Message = "Autosave of project {Project} failed; it stays unsaved and is retried.")]
    public static partial void AutosaveFailed(ILogger logger, string project, Exception exception);

    [LoggerMessage(EventId = 104, Level = LogLevel.Error, Message = "Project {Project} could not be written at shutdown; its latest changes are lost.")]
    public static partial void FlushFailed(ILogger logger, string project, Exception exception);
}
