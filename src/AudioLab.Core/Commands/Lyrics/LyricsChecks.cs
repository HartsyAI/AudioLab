using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The checks of what an aligner or a speech model answered, before its words are put into a clip.</summary>
internal static class LyricsChecks
{
    /// <summary>A refusal for an answer with no words, too many, or a value the project does not allow; null when all is well.</summary>
    public static CommandResult? Heard(ValueList<HeardWord> words, double? offset, string? model, string? source)
    {
        if (words.Count == 0 || words.Count > ProjectLimits.MaxListItems)
        {
            return CommandChecks.OutOfRange($"Name from 1 to {ProjectLimits.MaxListItems} words.");
        }
        foreach (HeardWord word in words)
        {
            CommandResult? invalid = CommandChecks.Length("word", word.Word, ProjectLimits.MaxName)
                ?? CommandChecks.Range("start", word.Start, 0, ProjectLimits.MaxSeconds)
                ?? CommandChecks.Range("end", word.End, 0, ProjectLimits.MaxSeconds);
            if (invalid is not null)
            {
                return invalid;
            }
        }
        return CommandChecks.Range(nameof(offset), offset, 0, ProjectLimits.MaxSeconds)
            ?? CommandChecks.Length(nameof(model), model, ProjectLimits.MaxLabel)
            ?? CommandChecks.Length(nameof(source), source, ProjectLimits.MaxLabel);
    }
}
