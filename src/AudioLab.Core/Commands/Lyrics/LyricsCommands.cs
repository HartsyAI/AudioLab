using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The lyrics commands: a clip's text and its timed words, in the seconds of the clip's source audio.</summary>
internal static class LyricsCommands
{
    public static void Register(CommandRegistryBuilder builder, LyricsCommandJsonContext json)
    {
        builder
            .Add("lyrics_set_text", "Sets the text of a clip's lyrics.", json.SetLyricsText, SetText)
            .Add("lyrics_apply_alignment", "Gives a clip lyrics of known text timed by an aligner.", json.ApplyLyricsAlignment, ApplyAlignment)
            .Add("lyrics_apply_transcript", "Gives a clip lyrics written from a transcript.", json.ApplyLyricsTranscript, ApplyTranscript)
            .Add("lyrics_estimate_timing", "Spreads a clip's lyrics evenly over the clip.", json.EstimateLyricsTiming, EstimateTiming)
            .Add("lyrics_clear_timing", "Drops the word times and keeps the text.", json.ClearLyricsTiming, ClearTiming)
            .Add("lyrics_shift", "Moves every word of a clip's lyrics.", json.ShiftLyrics, Shift)
            .Add("lyrics_set_word_time", "Sets the start or the end of one word.", json.SetLyricsWordTime, SetWordTime)
            .Add("lyrics_import_lrc", "Gives a clip the lyrics of an LRC file.", json.ImportLyricsLrc, ImportLrc);
    }

    private static CommandResult SetText(ProjectDocument document, SetLyricsText command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        if (CommandChecks.Length(nameof(command.Text), command.Text, ProjectLimits.MaxLongText) is { } tooLong)
        {
            return tooLong;
        }
        if (LyricsRules.WordsOf(command.Text).Count == 0)
        {
            return CommandResult.Ok(MetaEdits.SetLyrics(document, clip, null));
        }
        ClipLyrics current = clip.Meta?.Lyrics ?? new ClipLyrics { Text = string.Empty, Words = [], Source = LyricsRules.ManualSource };
        return CommandResult.Ok(MetaEdits.SetLyrics(document, clip, LyricsRules.SetText(current, command.Text)));
    }

    private static CommandResult ApplyAlignment(ProjectDocument document, ApplyLyricsAlignment command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        CommandResult? invalid = CommandChecks.Length(nameof(command.Text), command.Text, ProjectLimits.MaxLongText)
            ?? LyricsChecks.Heard(command.Words, command.Offset, command.Model, command.Source);
        if (invalid is not null)
        {
            return invalid;
        }
        if (LyricsRules.WordsOf(command.Text).Count == 0)
        {
            return CommandChecks.Rejected("There are no words in the text to time; write the lyrics from a transcript instead.");
        }
        double offset = command.Offset ?? clip.Offset;
        ClipLyrics lyrics = LyricsRules.FromAlignment(command.Text, Timed(command.Words), offset, command.Source, command.Model);
        return Apply(document, clip, lyrics);
    }

    private static CommandResult ApplyTranscript(ProjectDocument document, ApplyLyricsTranscript command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        if (LyricsChecks.Heard(command.Words, command.Offset, command.Model, command.Source, transcript: true) is { } invalid)
        {
            return invalid;
        }
        ClipLyrics lyrics = LyricsRules.FromTranscript(Timed(command.Words), command.Offset ?? clip.Offset, command.Source, command.Model);
        return Apply(document, clip, lyrics);
    }

    private static CommandResult EstimateTiming(ProjectDocument document, EstimateLyricsTiming command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        if (CommandChecks.Length(nameof(command.Text), command.Text, ProjectLimits.MaxLongText) is { } tooLong)
        {
            return tooLong;
        }
        string? text = command.Text ?? clip.Meta?.Lyrics?.Text;
        double visible = ClipRules.VisibleLength(clip);
        ValueList<LyricWord> words = LyricsRules.Estimate(text, clip.Offset, clip.Offset + visible);
        if (words.Count == 0)
        {
            return CommandChecks.Rejected("There are no lyrics to spread over the clip; write some first.");
        }
        ClipLyrics lyrics = new() { Text = text!, Words = words, Source = LyricsRules.EstimatedSource };
        return CommandResult.Ok(MetaEdits.SetLyrics(document, clip, lyrics));
    }

    private static CommandResult ClearTiming(ProjectDocument document, ClearLyricsTiming command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        return clip.Meta?.Lyrics is { } lyrics
            ? CommandResult.Ok(MetaEdits.SetLyrics(document, clip, LyricsRules.ClearTiming(lyrics)))
            : CommandResult.Ok(document);
    }

    private static CommandResult Shift(ProjectDocument document, ShiftLyrics command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        double limit = ProjectLimits.MaxSeconds;
        if (CommandChecks.Range(nameof(command.Seconds), command.Seconds, -limit, limit) is { } invalid)
        {
            return invalid;
        }
        return clip.Meta?.Lyrics is { } lyrics && LyricsRules.IsTimed(lyrics)
            ? CommandResult.Ok(MetaEdits.SetLyrics(document, clip, LyricsRules.Shift(lyrics, command.Seconds)))
            : CommandResult.Ok(document);
    }

    private static CommandResult SetWordTime(ProjectDocument document, SetLyricsWordTime command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        if (CommandChecks.Range(nameof(command.Word), command.Word, 0, ProjectLimits.MaxListItems - 1) is { } badIndex)
        {
            return badIndex;
        }
        ClipLyrics? lyrics = clip.Meta?.Lyrics;
        if (lyrics is null || command.Word >= lyrics.Words.Count)
        {
            return CommandChecks.NotFound("lyric word", command.Word.ToString(CultureInfo.InvariantCulture));
        }
        if (command.Start is null && command.End is null)
        {
            return CommandChecks.OutOfRange("Give start, end or both.");
        }
        CommandResult? invalid = CommandChecks.Range(nameof(command.Start), command.Start, 0, ProjectLimits.MaxSeconds)
            ?? CommandChecks.Range(nameof(command.End), command.End, 0, ProjectLimits.MaxSeconds);
        if (invalid is not null)
        {
            return invalid;
        }
        return CommandResult.Ok(MetaEdits.SetLyrics(document, clip, LyricsRules.SetWordTime(lyrics, command.Word, command.Start, command.End)));
    }

    private static CommandResult ImportLrc(ProjectDocument document, ImportLyricsLrc command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        CommandResult? invalid = CommandChecks.Length(nameof(command.Lrc), command.Lrc, ProjectLimits.MaxLongText)
            ?? CommandChecks.Range(nameof(command.LastLine), command.LastLine, 0, ProjectLimits.MaxSeconds);
        if (invalid is not null)
        {
            return invalid;
        }
        IReadOnlyList<LrcLine> parsed = LyricsRules.ParseLrc(command.Lrc, out bool tooLarge);
        if (tooLarge)
        {
            return CommandChecks.OutOfRange("That file's lines, counted once for each time stamp, hold more text than lyrics may.");
        }
        double lastLine = command.LastLine ?? LyricsRules.DefaultLastLine;
        ClipLyrics? lyrics = LyricsRules.FromLrc(parsed, time => time - clip.Start + clip.Offset, lastLine);
        if (lyrics is null)
        {
            return CommandChecks.Rejected("That file has no timed lyric lines.");
        }
        return lyrics.Words.Count == 0
            ? CommandChecks.Rejected("No line of that file falls inside the clip's audio, or its last line has no length.")
            : CommandResult.Ok(MetaEdits.SetLyrics(document, clip, lyrics));
    }

    // The words of a model's answer all go in; an answer none of whose words can be used changes nothing, so it is refused.
    private static CommandResult Apply(ProjectDocument document, AudioClip clip, ClipLyrics lyrics)
    {
        return lyrics.Words.Count == 0
            ? CommandChecks.Rejected("None of the words could be used: each was blank.")
            : CommandResult.Ok(MetaEdits.SetLyrics(document, clip, lyrics));
    }

    private static IEnumerable<TimedWord> Timed(ValueList<HeardWord> words) => words.Select(word => new TimedWord(word.Word, word.Start, word.End));
}
