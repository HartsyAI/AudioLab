using AudioLab.Core.Model;

namespace AudioLab.Audio.Midi;

/// <summary>Reads and writes Standard MIDI Files, between bytes and the notes and controllers of a MIDI clip; pure, so it runs in WebAssembly.</summary>
/// <remarks>
/// The reader takes format 0 and 1 files in ticks a quarter note and refuses the rest (format 2, SMPTE timing) and any file that is not
/// whole: a truncated chunk, a length past its chunk, a variable-length number of more than four bytes, a data byte with no running
/// status, an event after End of Track, a track with none, a tempo of 0 or over 1000 beats a minute, bytes after the last chunk, a
/// header that disagrees with the chunks, or more than the limits of <see cref="MidiLimits"/>. Sysex and meta events cancel running status.
/// A note-off with no note is counted and left out; a note never ended lasts to the end of its track. Every track and channel goes into
/// one list, so a clip is the whole file. The writer makes a format 0 file at 480 ticks a quarter note.
/// </remarks>
public static class MidiFile
{
    /// <summary>Reads a file, or throws <see cref="MidiFormatException"/> saying why it cannot.</summary>
    public static MidiImport Read(ReadOnlySpan<byte> bytes) => MidiReader.Read(bytes);

    /// <summary>
    /// Writes a clip as a file whose start is the clip's start, so the tempo that holds there is the file's first and the changes after it
    /// follow. <paramref name="bpm"/> and <paramref name="changes"/> are the project's, in seconds from the project's start.
    /// </summary>
    /// <remarks>
    /// Notes are cut at the clip's end and controllers after it are left out. A pedal (controller 64) still down at the end is let up
    /// there. A note with velocity 0 is written with 1, since 0 would end it. A tempo under 3.58 beats a minute is refused.
    /// </remarks>
    public static byte[] Write(MidiClip clip, double bpm, IReadOnlyList<TempoChange> changes, TimeSignature signature) =>
        MidiWriter.Write(clip, bpm, changes, signature);
}
