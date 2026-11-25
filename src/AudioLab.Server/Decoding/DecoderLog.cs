
namespace AudioLab.Server.Decoding;

/// <summary>Log lines of the decoder. Only versions, names and reasons are logged, never a path a user chose or a file's contents.</summary>
internal static partial class DecoderLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Decoder {Version} installed.")]
    public static partial void Installed(ILogger logger, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Decoder install failed: {Reason}")]
    public static partial void InstallFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Decoder refused to run: {Reason}")]
    public static partial void Refused(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Decode exited with code {ExitCode}; stderr: {Stderr}")]
    public static partial void DecodeEnded(ILogger logger, int exitCode, string stderr);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Decode was stopped: {Reason}")]
    public static partial void DecodeStopped(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A temporary decoder file was not removed: {Name} ({Error})")]
    public static partial void NotRemoved(ILogger logger, string name, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Decoder install stopped unexpectedly: {Error}")]
    public static partial void InstallStopped(ILogger logger, string error);
}
