namespace AudioLab.Web.Interop;

/// <summary>Hands key presses to one handler for the whole page, except those in a text field or a dialog, so typing is never a shortcut.</summary>
public interface IShortcutListener
{
    /// <summary>Routes key presses to <paramref name="handler"/>; true means it handled the press, which then loses its default action.</summary>
    public void Listen(Func<KeyChord, bool> handler);

    /// <summary>Stops routing key presses.</summary>
    public void Stop();
}
