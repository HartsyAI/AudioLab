using AudioLab.Web.Interop;

namespace AudioLab.Web.Session.Roll;

/// <summary>The hook between the page's one key listener and the piano roll: the roll sets a handler while it is open, and the page offers it each key first.</summary>
public sealed class PianoRollKeys
{
    /// <summary>Hears a key chord and says whether it was the roll's, which stops the browser from acting on it; null while no roll is open.</summary>
    public Func<KeyChord, bool>? Handler { get; set; }

    /// <summary>Offers a key chord to the roll.</summary>
    public bool Handle(KeyChord chord) => Handler?.Invoke(chord) ?? false;
}
