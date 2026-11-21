namespace AudioLab.Web.Session;

/// <summary>One MIDI note inside a clip, as percentages of the clip body.</summary>
public readonly record struct NoteSpan(float X, float Y, float Width);
