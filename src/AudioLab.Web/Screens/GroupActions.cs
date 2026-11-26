using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Screens;

/// <summary>What the mixer can do to a track group; every edit answers with what it came to, so a caller can follow it.</summary>
public sealed class GroupActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Folds a group to one row, or shows its tracks.</summary>
    public Func<GroupId, bool, Task<EditResult>> SetCollapsed { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets the gain multiplier of a group, from 0 up.</summary>
    public Func<GroupId, double, Task<EditResult>> SetVolume { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Mutes or unmutes every member.</summary>
    public Func<GroupId, bool, Task<EditResult>> SetMuted { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Solos or unsolos every member.</summary>
    public Func<GroupId, bool, Task<EditResult>> SetSoloed { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Chooses whether the clips of a group draw in its colour.</summary>
    public Func<GroupId, bool, Task<EditResult>> SetTint { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Recolours a group.</summary>
    public Func<GroupId, string, Task<EditResult>> SetColor { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static GroupActions For(GroupEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new GroupActions
        {
            SetCollapsed = (group, collapsed) => show(editor.SetCollapsedAsync(group, collapsed)),
            SetVolume = (group, volume) => show(editor.SetVolumeAsync(group, volume)),
            SetMuted = (group, muted) => show(editor.SetMutedAsync(group, muted)),
            SetSoloed = (group, soloed) => show(editor.SetSoloedAsync(group, soloed)),
            SetTint = (group, tint) => show(editor.SetTintAsync(group, tint)),
            SetColor = (group, color) => show(editor.SetColorAsync(group, color)),
        };
    }
}
