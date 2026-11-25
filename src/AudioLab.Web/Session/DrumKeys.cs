using AudioLab.Web.Interop;

namespace AudioLab.Web.Session;

/// <summary>The hook between the page key listener and the drums tab: the tab sets a handler while it is open and says which keys its grid takes first.</summary>
public sealed class DrumKeys
{
    /// <summary>Says whether a key chord is the grid's, which stops the page acting on it; null while no drums tab is open.</summary>
    public Func<KeyChord, bool>? Handler { get; set; }

    /// <summary>Offers a key chord to the drums tab and says whether it took it.</summary>
    public bool Handle(KeyChord chord) => Handler?.Invoke(chord) ?? false;
}
