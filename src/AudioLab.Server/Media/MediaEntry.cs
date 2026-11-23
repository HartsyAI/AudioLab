using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>One file in a user's index: its record, and whether a delete of it has begun.</summary>
internal sealed class MediaEntry(MediaRecord record)
{
    public MediaRecord Record { get; } = record;

    public bool PendingDelete { get; set; }
}
