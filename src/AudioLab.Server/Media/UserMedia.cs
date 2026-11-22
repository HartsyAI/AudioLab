using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>What the store keeps in memory for one user: the index of their files and what their running uploads have taken.</summary>
internal sealed class UserMedia
{
    public object Sync { get; } = new();

    public SemaphoreSlim Gate { get; } = new(1, 1);

    public Dictionary<MediaId, MediaEntry> Entries { get; } = [];

    public bool Loaded { get; set; }

    public int Refused { get; set; }

    public long TotalBytes { get; set; }

    public int UploadsRunning { get; set; }

    public long UploadBytes { get; set; }

    public List<string> Unreadable { get; } = [];

    public DateTimeOffset LastAttempt { get; set; }
}
