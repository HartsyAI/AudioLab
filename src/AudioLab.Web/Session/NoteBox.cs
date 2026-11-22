namespace AudioLab.Web.Session;

/// <summary>Where one note of a MIDI clip is drawn inside the clip's body, in percent of the body.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge, from the highest pitch of the clip at the top.</param>
/// <param name="Width">The width.</param>
public readonly record struct NoteBox(double X, double Y, double Width);
