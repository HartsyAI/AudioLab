namespace AudioLab.Server.Midi;

/// <summary>A .mid file of a clip, with a name for it that holds no path or control character.</summary>
/// <param name="Bytes">The Standard MIDI File.</param>
/// <param name="FileName">The file's name, with its extension.</param>
public sealed record MidiExport(byte[] Bytes, string FileName);
