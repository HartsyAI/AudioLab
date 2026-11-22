using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Lays the real notes of a MIDI clip out inside its body: time across, pitch down, the clip's own range over the height.</summary>
public static class MidiNoteLayout
{
    private const double Top = 10;

    private const double Span = 80;

    private const double MinWidth = 0.6;

    /// <summary>One box for each note, in the clip's note order.</summary>
    public static NoteBox[] Boxes(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.Notes.Count == 0 || clip.Length <= 0)
        {
            return [];
        }
        int high = clip.Notes.Max(note => note.Pitch);
        int low = clip.Notes.Min(note => note.Pitch);
        double range = Math.Max(1, high - low);
        return
        [
            .. clip.Notes.Select(note => new NoteBox(
                Math.Clamp(note.Start / clip.Length * 100, 0, 100),
                Top + ((high - note.Pitch) / range * Span),
                Math.Max(MinWidth, Math.Min(note.Duration, clip.Length - note.Start) / clip.Length * 100))),
        ];
    }
}
