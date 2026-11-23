using AudioLab.Web.Interop;

namespace AudioLab.Web.Session.Roll;

/// <summary>The hook between the page key listener and the piano roll: the roll sets a handler while it is open and gets keys first.</summary>
public sealed class PianoRollKeys
{
    /// <summary>Hears a key chord and says whether it was the roll's, which stops the browser acting on it; null while no roll is open.</summary>
    public Func<KeyChord, bool>? Handler { get; set; }

    /// <summary>Offers a key chord to the roll.</summary>
    public bool Handle(KeyChord chord) => Handler?.Invoke(chord) ?? false;

    /// <summary>Offers a key chord to the roll first and to the page's other keys, the transport's, only when the roll did not take it.</summary>
    /// <remarks>
    /// The order matters: the roll's Shift with an arrow and the transport's bar seek are the same chord, and the roll has it while its focus is in
    /// the roll.
    /// </remarks>
    public bool Route(KeyChord chord, Func<KeyChord, bool> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Handle(chord) || page(chord);
    }
}
