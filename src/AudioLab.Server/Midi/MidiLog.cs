namespace AudioLab.Server.Midi;

/// <summary>The log messages of MIDI import and export.</summary>
internal static partial class MidiLog
{
    [LoggerMessage(EventId = 400, Level = LogLevel.Information, Message = "A MIDI file of {Bytes} bytes was refused: {Reason}")]
    public static partial void FileRefused(ILogger logger, long bytes, string reason);
}
