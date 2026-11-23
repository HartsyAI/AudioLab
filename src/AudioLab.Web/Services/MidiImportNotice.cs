using System.Globalization;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Services;

/// <summary>What the user is told about an import: the file's tempo that the project did not take, and what the reader left out.</summary>
/// <remarks>The project's tempo is never changed by an import (docs/midi.md): the notes keep the file's seconds, so the user must be told.</remarks>
public static class MidiImportNotice
{
    private const string NotChanged = "the project's tempo was not changed, so the notes keep the file's seconds and do not fall on its beats.";

    /// <summary>The notice for an import, or null when the file's tempo is the project's and nothing was left out.</summary>
    /// <param name="result">The server's answer.</param>
    /// <param name="document">The project as the page holds it, for the tempo where the clip starts.</param>
    /// <param name="start">Where the clip was placed, in seconds.</param>
    public static string? Of(MidiImportResult result, ProjectDocument document, double start)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(document);
        List<string> sentences = [];
        double project = TempoMap.From(document).BpmAt(start);
        if (Math.Abs(result.Bpm - project) >= 0.01)
        {
            sentences.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"The file's tempo is {result.Bpm:0.##} bpm and the project's is {project:0.##}; {NotChanged}"));
        }
        if (result.TempoChanges > 0)
        {
            sentences.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"The file changes tempo {Times(result.TempoChanges)} after it starts; the project does not follow them."));
        }
        if (result.IgnoredNoteOffs > 0)
        {
            sentences.Add(
                string.Create(CultureInfo.InvariantCulture, $"{result.IgnoredNoteOffs} note-off events had no note to end and were left out."));
        }
        if (result.UnclosedNotes > 0)
        {
            sentences.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{result.UnclosedNotes} notes were never ended in the file and last to the end of their track."));
        }
        return sentences.Count == 0 ? null : string.Join(' ', sentences);
    }

    private static string Times(int count) => count == 1 ? "once" : string.Create(CultureInfo.InvariantCulture, $"{count} times");
}
