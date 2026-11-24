using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Services;

/// <summary>A key chord that runs one command; the menus, the tooltips and the key handlers all read it from <see cref="Shortcuts"/>.</summary>
/// <param name="Id">The command's id.</param>
/// <param name="Name">The command's name, in sentence case.</param>
/// <param name="Key">The key as the browser names it in <c>KeyboardEvent.key</c>, such as F2 or Delete.</param>
/// <param name="Scope">The screen it belongs to; two commands may share a chord only on different screens.</param>
/// <param name="Ctrl">Whether Control (or Command) is held.</param>
/// <param name="Shift">Whether Shift is held.</param>
/// <param name="Alt">Whether Alt is held.</param>
public sealed record Shortcut(string Id, string Name, string Key, string Scope, bool Ctrl = false, bool Shift = false, bool Alt = false)
{
    /// <summary>What the command key is called here: Ctrl, or Cmd on an Apple system. The page sets it once at start.</summary>
    public static string CommandKey { get; set; } = "Ctrl";

    /// <summary>The chord as a person reads it, such as Ctrl+Shift+Z (Cmd+Shift+Z on an Apple system).</summary>
    public string Display =>
        (Ctrl ? CommandKey + "+" : string.Empty) + (Alt ? "Alt+" : string.Empty) + (Shift ? "Shift+" : string.Empty)
        + (Key == " " ? "Space" : Key.Length == 1 ? Key.ToUpperInvariant() : Key);

    /// <summary>Whether a key press is this chord.</summary>
    public bool Matches(KeyboardEventArgs press)
    {
        ArgumentNullException.ThrowIfNull(press);
        return Matches(new KeyChord(press.Key, press.CtrlKey || press.MetaKey, press.ShiftKey, press.AltKey));
    }

    /// <summary>Whether a key press is this chord.</summary>
    public bool Matches(KeyChord press)
    {
        return string.Equals(press.Key, Key, StringComparison.OrdinalIgnoreCase) && press.Ctrl == Ctrl && press.Shift == Shift && press.Alt == Alt;
    }
}
