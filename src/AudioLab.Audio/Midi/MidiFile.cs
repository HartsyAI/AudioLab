using AudioLab.Core.Model;

namespace AudioLab.Audio.Midi;

/// <summary>Reads and writes Standard MIDI Files between bytes and a MIDI clip; pure, so it runs in WebAssembly.</summary>
/// <remarks>The limits and refusals are those of <see cref="MidiLimits"/>; every track and channel goes into one list.</remarks>
public static class MidiFile
{
    /// <summary>Reads a file, or throws <see cref="MidiFormatException"/> saying why it cannot.</summary>
    public static MidiImport Read(ReadOnlySpan<byte> bytes) => MidiReader.Read(bytes);

    /// <summary>Writes a clip as a file that starts where the clip does; the tempo map is the project's, from the project's start.</summary>
    /// <remarks>Notes are cut at the clip's end and a pedal still down there is let up; a tempo under 3.58 beats a minute is refused.</remarks>
    public static byte[] Write(MidiClip clip, double bpm, IReadOnlyList<TempoChange> changes, TimeSignature signature) =>
        MidiWriter.Write(clip, bpm, changes, signature);
}
