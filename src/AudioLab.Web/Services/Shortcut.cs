using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Services;

/// <summary>A key chord that runs one command; the menus, the tooltips and the key handler all read it from <see cref="Shortcuts"/>.</summary>
/// <param name="Id">The command's id.</param>
/// <param name="Name">The command's name, in sentence case.</param>
/// <param name="Key">The key as the browser names it in <c>KeyboardEvent.key</c>, such as F2 or Delete.</param>
/// <param name="Ctrl">Whether Control (or Command) is held.</param>
/// <param name="Shift">Whether Shift is held.</param>
public sealed record Shortcut(string Id, string Name, string Key, bool Ctrl = false, bool Shift = false)
{
    /// <summary>The chord as a person reads it, such as Ctrl+Z.</summary>
    public string Display => (Ctrl ? "Ctrl+" : string.Empty) + (Shift ? "Shift+" : string.Empty) + (Key.Length == 1 ? Key.ToUpperInvariant() : Key);

    /// <summary>Whether a key press is this chord.</summary>
    public bool Matches(KeyboardEventArgs press)
    {
        ArgumentNullException.ThrowIfNull(press);
        return string.Equals(press.Key, Key, StringComparison.OrdinalIgnoreCase)
            && (press.CtrlKey || press.MetaKey) == Ctrl
            && press.ShiftKey == Shift
            && !press.AltKey;
    }
}
