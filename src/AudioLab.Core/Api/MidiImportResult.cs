using System.Text.Json.Serialization;

namespace AudioLab.Core.Api;

/// <summary>What a client is told after a MIDI file became a clip.</summary>
/// <param name="Clip">The id of the new clip.</param>
/// <param name="Revision">The project's revision once the clip is in.</param>
/// <param name="Notes">How many notes the clip holds.</param>
/// <param name="ControlChanges">How many controller events the clip holds.</param>
/// <param name="Length">The clip's length in seconds: the file's.</param>
/// <param name="Bpm">The tempo the file starts at, which the project does not take over.</param>
/// <param name="TempoChanges">How many tempo changes the file has after its start, which the project does not take over.</param>
/// <param name="IgnoredNoteOffs">How many note-off events had no note to end and were left out.</param>
/// <param name="UnclosedNotes">How many notes were never ended in the file and last to the end of their track.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MidiImportResult(
    string Clip,
    long Revision,
    int Notes,
    int ControlChanges,
    double Length,
    double Bpm,
    int TempoChanges,
    int IgnoredNoteOffs,
    int UnclosedNotes);
