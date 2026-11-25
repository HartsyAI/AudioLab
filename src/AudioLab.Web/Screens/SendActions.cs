using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Screens;

/// <summary>What the mixer can do to a track's sends; every edit answers with what it came to, so a caller can follow it.</summary>
public sealed class SendActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Routes a track to a bus.</summary>
    public Func<TrackId, TrackId, Task<EditResult>> Add { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Removes a track's send to a bus.</summary>
    public Func<TrackId, TrackId, Task<EditResult>> Remove { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets the gain of a send, from 0 up.</summary>
    public Func<TrackId, TrackId, double, Task<EditResult>> SetLevel { get; init; } = (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Mutes or unmutes a send.</summary>
    public Func<TrackId, TrackId, bool, Task<EditResult>> SetMuted { get; init; } = (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Takes a send before or after the fader.</summary>
    public Func<TrackId, TrackId, bool, Task<EditResult>> SetPre { get; init; } = (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static SendActions For(SendEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new SendActions
        {
            Add = (track, bus) => show(editor.AddAsync(track, bus)),
            Remove = (track, bus) => show(editor.RemoveAsync(track, bus)),
            SetLevel = (track, bus, level) => show(editor.SetLevelAsync(track, bus, level)),
            SetMuted = (track, bus, muted) => show(editor.SetMutedAsync(track, bus, muted)),
            SetPre = (track, bus, pre) => show(editor.SetPreAsync(track, bus, pre)),
        };
    }
}
