namespace AudioLab.Web.Interop;

/// <summary>A key press as the shortcut listener hands it up: the key and which modifiers were held.</summary>
/// <param name="Key">The key as the browser names it, such as z or F2.</param>
/// <param name="Ctrl">Whether Control or Command was held.</param>
/// <param name="Shift">Whether Shift was held.</param>
/// <param name="Alt">Whether Alt was held.</param>
/// <param name="Repeat">Whether the key is held down and this press is the browser repeating it.</param>
public readonly record struct KeyChord(string Key, bool Ctrl, bool Shift, bool Alt, bool Repeat = false);
