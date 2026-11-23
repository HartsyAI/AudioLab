using AudioLab.Core.Model;
using AudioLab.Web.Media;
using AudioLab.Web.Session;

namespace AudioLab.Web.Screens;

/// <summary>What the session screen can do to the project, handed down by the page that owns the connection, and why not when it cannot.</summary>
public sealed class SessionActions
{
    /// <summary>A set of actions that does nothing, with the reason; what the screen shows before the project is live.</summary>
    public static SessionActions ReadOnly(string reason, Func<Task>? reconnect = null)
    {
        return new SessionActions { CanEdit = false, Reason = reason, Reconnect = reconnect };
    }

    /// <summary>Whether the project may be edited from here now.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Why it may not, for the tooltip of a control that would edit it; null when it may.</summary>
    public string? Reason { get; init; }

    /// <summary>Connects again and rejoins the project, or null when that cannot help (it is connected, or the project is gone).</summary>
    public Func<Task>? Reconnect { get; init; }

    /// <summary>Adds a track.</summary>
    public Func<Task> AddTrack { get; init; } = () => Task.CompletedTask;

    /// <summary>Asks for the new name of a track.</summary>
    public Func<TrackId, Task> RenameTrack { get; init; } = _ => Task.CompletedTask;

    /// <summary>Toggles the mute of a track.</summary>
    public Func<TrackId, Task> ToggleMute { get; init; } = _ => Task.CompletedTask;

    /// <summary>Toggles the solo of a track.</summary>
    public Func<TrackId, Task> ToggleSolo { get; init; } = _ => Task.CompletedTask;

    /// <summary>Moves a clip.</summary>
    public Func<ClipMove, Task> MoveClip { get; init; } = _ => Task.CompletedTask;

    /// <summary>Adds a media file as a clip at a place on an audio track.</summary>
    public Func<MediaPlacement, Task> AddClip { get; init; } = _ => Task.CompletedTask;

    /// <summary>Turns the metronome on or off.</summary>
    public Func<bool, Task> SetMetronome { get; init; } = _ => Task.CompletedTask;

    /// <summary>Goes back one undo step.</summary>
    public Func<Task> Undo { get; init; } = () => Task.CompletedTask;

    /// <summary>Goes forward one redo step.</summary>
    public Func<Task> Redo { get; init; } = () => Task.CompletedTask;
}
