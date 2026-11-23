namespace AudioLab.Server.Media;

/// <summary>A record file that could not be read: when it is tried again, and how big its bytes are, which count against the quota meanwhile.</summary>
internal sealed class UnreadableRecord(string path, long bytes, DateTimeOffset nextAttempt)
{
    public string Path { get; } = path;

    public long Bytes { get; } = bytes;

    public int Attempts { get; set; } = 1;

    public DateTimeOffset NextAttempt { get; set; } = nextAttempt;

    public bool Terminal { get; set; }
}
