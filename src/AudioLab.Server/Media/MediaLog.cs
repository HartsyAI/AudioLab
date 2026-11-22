namespace AudioLab.Server.Media;

/// <summary>The log messages of the media store. File names are the store's own hashed names, never an id or a user's text.</summary>
internal static partial class MediaLog
{
    [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "The media store refused the record {File}: {Problems}")]
    public static partial void RecordRefused(ILogger logger, string file, string problems);

    [LoggerMessage(EventId = 201, Level = LogLevel.Information, Message = "Removed {Count} media files that had no record.")]
    public static partial void OrphansSwept(ILogger logger, int count);

    [LoggerMessage(EventId = 202, Level = LogLevel.Warning, Message = "The media file {File} could not be removed: {Reason}")]
    public static partial void NotRemoved(ILogger logger, string file, string reason);

    [LoggerMessage(EventId = 203, Level = LogLevel.Error, Message = "The media folder could not be read: {Reason}; its files are treated as missing until it can.")]
    public static partial void FolderUnreadable(ILogger logger, string reason);
}
