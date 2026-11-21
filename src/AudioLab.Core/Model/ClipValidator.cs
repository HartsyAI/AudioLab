namespace AudioLab.Core.Model;

/// <summary>The clip rules: audio clips and what they carry, and MIDI clips with their notes and controllers.</summary>
internal static class ClipValidator
{
    private static readonly string[] _clipMetaMembers = ["score", "lyrics", "stemOf"];

    private static readonly string[] _scoreMembers =
    [
        "abc", "truncated", "budgetSeconds", "style", "lyrics", "cot", "seed", "model", "engineId", "label", "parent",
        "source", "created",
    ];

    private static readonly string[] _lyricsMembers = ["text", "words", "source", "model"];

    /// <summary>Checks one audio clip; its fades must fit inside the part of the file that plays.</summary>
    public static void CheckAudioClip(
        AudioClip clip,
        string path,
        HashSet<string> clipIds,
        HashSet<string> audioClipIds,
        ProblemList problems)
    {
        problems.Unique(clipIds, clip.Id.Value, $"{path}.id");
        problems.Id(clip.Media.Value, $"{path}.media");
        problems.Text(clip.Name, ProjectLimits.MaxName, $"{path}.name", required: false);
        if (clip.Color is not null)
        {
            problems.Color(clip.Color, $"{path}.color");
        }
        problems.Range(clip.Start, 0, ProjectLimits.MaxSeconds, $"{path}.start");
        problems.Positive(clip.Duration, ProjectLimits.MaxSeconds, $"{path}.duration");
        problems.Range(clip.Offset, 0, ProjectLimits.MaxSeconds, $"{path}.offset");
        problems.Range(clip.TrimEnd, 0, ProjectLimits.MaxSeconds, $"{path}.trimEnd");
        problems.Range(clip.Gain, 0, ProjectLimits.MaxGain, $"{path}.gain");
        double playable = clip.Duration - clip.Offset - clip.TrimEnd;
        bool known = double.IsFinite(playable) && clip.Duration > 0 && clip.Offset >= 0 && clip.TrimEnd >= 0;
        if (known && playable <= 0)
        {
            problems.Add($"{path}: nothing of the clip plays; the offset and trim must leave part of the duration.");
        }
        double longest = known ? Math.Max(playable, 0) : 0;
        problems.Range(clip.FadeIn, 0, longest, $"{path}.fadeIn");
        problems.Range(clip.FadeOut, 0, longest, $"{path}.fadeOut");
        if (clip.Meta is not null)
        {
            CheckMeta(clip.Meta, clip.Id.Value, $"{path}.meta", audioClipIds, problems);
        }
    }

    /// <summary>Checks one MIDI clip with its notes and controller events.</summary>
    public static void CheckMidiClip(MidiClip clip, string path, HashSet<string> clipIds, ProblemList problems)
    {
        problems.Unique(clipIds, clip.Id.Value, $"{path}.id");
        problems.Text(clip.Name, ProjectLimits.MaxName, $"{path}.name", required: false);
        if (clip.Color is not null)
        {
            problems.Color(clip.Color, $"{path}.color");
        }
        problems.Range(clip.Start, 0, ProjectLimits.MaxSeconds, $"{path}.start");
        problems.Range(clip.Length, MidiClip.MinLength, ProjectLimits.MaxSeconds, $"{path}.length");
        problems.Range(clip.NextNoteId.Value, 1, NoteId.Max, $"{path}.nextNoteId");
        problems.Count(clip.Notes.Count, ProjectLimits.MaxListItems, $"{path}.notes");
        problems.Count(clip.ControlChanges.Count, ProjectLimits.MaxListItems, $"{path}.controlChanges");
        CheckNotes(clip, path, problems);
        double previous = 0;
        double latest = Math.Min(clip.Length, ProjectLimits.MaxSeconds);
        for (int i = 0; i < clip.ControlChanges.Count; i++)
        {
            MidiControlChange change = clip.ControlChanges[i];
            string changePath = $"{path}.controlChanges[{i}]";
            problems.Range(change.Time, previous, latest, $"{changePath}.time");
            problems.Range(change.Controller, 0, 127, $"{changePath}.controller");
            problems.Range(change.Value, 0, 127, $"{changePath}.value");
            previous = double.IsFinite(change.Time) ? Math.Max(previous, change.Time) : previous;
        }
    }

    // A note starts inside its clip; it may ring on past the clip end, as a held or pedalled note does.
    private static void CheckNotes(MidiClip clip, string path, ProblemList problems)
    {
        HashSet<int> noteIds = [];
        double latest = Math.Min(clip.Length, ProjectLimits.MaxSeconds);
        foreach (MidiNote note in clip.Notes)
        {
            string notePath = $"{path}.notes[{note.Id.Value}]";
            if (note.Id.Value < 1 || note.Id.Value >= clip.NextNoteId.Value || !noteIds.Add(note.Id.Value))
            {
                problems.Add($"{notePath}.id: a note id is 1 or more, unique in its clip and below nextNoteId.");
            }
            problems.Range(note.Pitch, 0, 127, $"{notePath}.pitch");
            problems.Range(note.Velocity, 0, 127, $"{notePath}.velocity");
            problems.Range(note.Duration, 0, ProjectLimits.MaxSeconds, $"{notePath}.duration");
            if (!(note.Start >= 0 && note.Start < latest))
            {
                problems.Add($"{notePath}.start: a note starts inside its clip, from 0 up to its length.");
            }
        }
    }

    private static void CheckMeta(
        ClipMeta meta,
        string clipId,
        string path,
        HashSet<string> audioClipIds,
        ProblemList problems)
    {
        CheckExtra(meta.Extra, _clipMetaMembers, $"{path}.extra", problems);
        if (meta.Score is { } score)
        {
            CheckScore(score, clipId, $"{path}.score", audioClipIds, problems);
        }
        if (meta.Lyrics is { } lyrics)
        {
            CheckLyrics(lyrics, $"{path}.lyrics", problems);
        }
        if (meta.StemOf is { } stem)
        {
            problems.Text(stem.Stem, ProjectLimits.MaxLabel, $"{path}.stemOf.stem");
            CheckLink(stem.ClipId, clipId, $"{path}.stemOf.clipId", audioClipIds, problems);
        }
    }

    private static void CheckScore(
        ScoreMeta score,
        string clipId,
        string path,
        HashSet<string> audioClipIds,
        ProblemList problems)
    {
        Long(score.Abc, $"{path}.abc", problems);
        Long(score.Style, $"{path}.style", problems);
        Long(score.Lyrics, $"{path}.lyrics", problems);
        Short(score.Cot, ProjectLimits.MaxLabel, $"{path}.cot", problems);
        Short(score.Model, ProjectLimits.MaxLabel, $"{path}.model", problems);
        Short(score.EngineId, ProjectLimits.MaxLabel, $"{path}.engineId", problems);
        Short(score.Label, ProjectLimits.MaxName, $"{path}.label", problems);
        Short(score.Source, ProjectLimits.MaxLabel, $"{path}.source", problems);
        if (score.BudgetSeconds is { } budget)
        {
            problems.Range(budget, 0, ProjectLimits.MaxSeconds, $"{path}.budgetSeconds");
        }
        if (score.Created is < 0)
        {
            problems.Add($"{path}.created: a time since 1970 is not negative.");
        }
        CheckExtra(score.Extra, _scoreMembers, $"{path}.extra", problems);
        CheckLink(score.Parent, clipId, $"{path}.parent", audioClipIds, problems);
    }

    private static void CheckLyrics(ClipLyrics lyrics, string path, ProblemList problems)
    {
        problems.Text(lyrics.Text, ProjectLimits.MaxLongText, $"{path}.text", required: false, allowLineBreaks: true);
        Short(lyrics.Source, ProjectLimits.MaxLabel, $"{path}.source", problems);
        Short(lyrics.Model, ProjectLimits.MaxLabel, $"{path}.model", problems);
        CheckExtra(lyrics.Extra, _lyricsMembers, $"{path}.extra", problems);
        problems.Count(lyrics.Words.Count, ProjectLimits.MaxListItems, $"{path}.words");
        for (int i = 0; i < lyrics.Words.Count; i++)
        {
            LyricWord word = lyrics.Words[i];
            string wordPath = $"{path}.words[{i}]";
            problems.Text(word.Word, ProjectLimits.MaxName, $"{wordPath}.w");
            problems.Range(word.Start, 0, ProjectLimits.MaxSeconds, $"{wordPath}.s");
            double from = double.IsFinite(word.Start) ? word.Start : 0;
            problems.Range(word.End, from, ProjectLimits.MaxSeconds, $"{wordPath}.e");
            if (word.Line is < 0 or > 1_000_000)
            {
                problems.Add($"{wordPath}.line: a line index is 0 to 1000000.");
            }
        }
    }

    // Unknown fields are written beside the known ones in one JSON object, so a name already in use would appear twice.
    private static void CheckExtra(ExtraFields extra, string[] known, string path, ProblemList problems)
    {
        problems.Count(extra.Count, ProjectLimits.MaxExtraFields, path);
        if (extra.TotalLength > ProjectLimits.MaxExtraLength)
        {
            long limit = ProjectLimits.MaxExtraLength;
            problems.Add($"{path}: the unknown fields take {extra.TotalLength} characters; at most {limit}.");
        }
        foreach (string name in extra.Names.Where(name => known.Contains(name, StringComparer.Ordinal)))
        {
            problems.Add($"{path}.{name}: an unknown field cannot reuse the name of a known one.");
        }
    }

    private static void CheckLink(
        ClipId? link,
        string ownId,
        string path,
        HashSet<string> audioClipIds,
        ProblemList problems)
    {
        if (link is not { } target)
        {
            return;
        }
        if (target.Value == ownId)
        {
            problems.Add($"{path}: a clip cannot link to itself.");
        }
        else if (!audioClipIds.Contains(target.Value ?? string.Empty))
        {
            problems.Add($"{path}: {target.Value} is not an audio clip of this project.");
        }
    }

    private static void Long(string? value, string path, ProblemList problems)
    {
        if (value is not null)
        {
            problems.Text(value, ProjectLimits.MaxLongText, path, required: false, allowLineBreaks: true);
        }
    }

    private static void Short(string? value, int max, string path, ProblemList problems)
    {
        if (value is not null)
        {
            problems.Text(value, max, path, required: false);
        }
    }
}
