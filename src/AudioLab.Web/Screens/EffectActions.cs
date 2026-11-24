using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;

namespace AudioLab.Web.Screens;

/// <summary>What the session screen can do to a track's effect chain; every edit answers with what it came to, so a caller can follow it.</summary>
public sealed class EffectActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Adds an effect of a kind at the end of a track's chain.</summary>
    public Func<TrackId, EffectKind, Task<EditResult>> Add { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Removes an effect from a track's chain.</summary>
    public Func<TrackId, EffectId, Task<EditResult>> Remove { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Puts an effect in the signal path (true) or bypasses it.</summary>
    public Func<TrackId, EffectId, bool, Task<EditResult>> SetEnabled { get; init; } =
        (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Moves an effect by places in its chain, negative for earlier, as one gesture.</summary>
    public Func<TrackId, EffectId, int, Task<EditResult>> Move { get; init; } = (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets one numeric setting of an effect to an absolute value.</summary>
    public Func<TrackId, EffectId, string, double, Task<EditResult>> SetParameter { get; init; } =
        (_, _, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets whether a delay follows the tempo, and its division when one is given.</summary>
    public Func<TrackId, EffectId, bool, string?, Task<EditResult>> SetDelaySync { get; init; } =
        (_, _, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Replaces an effect's settings with a preset's.</summary>
    public Func<TrackId, EffectId, string, Task<EditResult>> ApplyPreset { get; init; } =
        (_, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static EffectActions For(EffectEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new EffectActions
        {
            Add = (track, kind) => show(editor.AddAsync(track, kind)),
            Remove = (track, effect) => show(editor.RemoveAsync(track, effect)),
            SetEnabled = (track, effect, enabled) => show(editor.SetEnabledAsync(track, effect, enabled)),
            Move = (track, effect, places) => show(editor.MoveAsync(track, effect, places)),
            SetParameter = (track, effect, name, value) => show(editor.SetParameterAsync(track, effect, name, value)),
            SetDelaySync = (track, effect, sync, division) => show(editor.SetDelaySyncAsync(track, effect, sync, division)),
            ApplyPreset = (track, effect, preset) => show(editor.ApplyPresetAsync(track, effect, preset)),
        };
    }
}
