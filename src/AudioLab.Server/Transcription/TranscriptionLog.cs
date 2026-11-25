
namespace AudioLab.Server.Transcription;

/// <summary>The log lines of the transcription jobs; a message names no file, user or media id.</summary>
internal static partial class TranscriptionLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "A transcription job state file was left out of the table.")]
    public static partial void StateSkipped(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "A transcription store write failed: {Type}.")]
    public static partial void StoreFailed(ILogger logger, string type);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "A transcription resumed at chunk {Done} of {Chunks}.")]
    public static partial void Resumed(ILogger logger, int done, int chunks);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "A model call is retried, attempt {Attempt}.")]
    public static partial void ModelRetried(ILogger logger, int attempt);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "The transcription worker stopped a turn: {Type}.")]
    public static partial void WorkerFailed(ILogger logger, string type);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "A transcription chunk log had a damaged tail of {Bytes} bytes; it was cut off.")]
    public static partial void DamagedTail(ILogger logger, long bytes);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "A transcription file could not be read or removed: {Type}.")]
    public static partial void FileFailed(ILogger logger, string type);
}
