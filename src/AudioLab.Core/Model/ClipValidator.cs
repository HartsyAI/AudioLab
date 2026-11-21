namespace AudioLab.Core.Model;

/// <summary>The clip rules: audio clips and what they carry, and MIDI clips with their notes and controllers.</summary>
internal static class ClipValidator
{
    private const double Tolerance = 1e-9;

    private const int MaxMetaFields = 256;

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
        if (double.IsFinite(playable) && playable <= 0)
        {
            problems.Add($"{path}: nothing of the clip plays; the offset and trim must leave part of the duration.");
        }
        double longest = double.IsFinite(playable) ? Math.Max(playable, 0) : 0;
        problems.Range(clip.FadeIn, 0, longest, $"{path}.fadeIn");
        problems.Range(clip.FadeOut, 0, longest, $"{path}.fadeOut");
        if (clip.Meta is not null)
        {
            CheckMeta(clip.Meta, clip.Id.Value, $"{path}.meta", audioClipIds, problems);
        }
    }

    public static void CheckMidiClip(MidiClip clip, string path, HashSet<string> clipIds, ProblemList problems)
    {
        problems.Unique(clipIds, clip.Id.Value, $"{path}.id");
        problems.Text(clip.Name, ProjectLimits.MaxName, $"{path}.name", required: false);
        if (clip.Color is not null)
        {
            problems.Color(clip.Color, $"{path}.color");
        }
        problems.Range(clip.Start, 0, ProjectLimits.MaxSeconds, $"{path}.start");
        problems.Positive(clip.Length, ProjectLimits.MaxSeconds, $"{path}.length");
        problems.Range(clip.NextNoteId.Value, 1, int.MaxValue, $"{path}.nextNoteId");
        CheckNotes(clip, path, problems);
        double previous = 0;
        for (int i = 0; i < clip.ControlChanges.Count; i++)
        {
            MidiControlChange change = clip.ControlChanges[i];
            string changePath = $"{path}.controlChanges[{i}]";
            double latest = Math.Min(clip.Length, ProjectLimits.MaxSeconds);
            problems.Range(change.Time, previous, latest, $"{changePath}.time");
            problems.Range(change.Controller, 0, 127, $"{changePath}.controller");
            problems.Range(change.Value, 0, 127, $"{changePath}.value");
            previous = double.IsFinite(change.Time) ? Math.Max(previous, change.Time) : previous;
        }
    }

    private static void CheckNotes(MidiClip clip, string path, ProblemList problems)
    {
        HashSet<int> noteIds = [];
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
            problems.Range(note.Start, 0, Math.Min(clip.Length, ProjectLimits.MaxSeconds), $"{notePath}.start");
            if (note.Start >= clip.Length)
            {
                problems.Add($"{notePath}.start: a note starts inside its clip.");
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
        problems.Range(meta.Extra.Count, 0, MaxMetaFields, $"{path}.extra");
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
        Optional(score.Abc, ProjectLimits.MaxLongText, $"{path}.abc", problems);
        Optional(score.Style, ProjectLimits.MaxLongText, $"{path}.style", problems);
        Optional(score.Lyrics, ProjectLimits.MaxLongText, $"{path}.lyrics", problems);
        Optional(score.Cot, ProjectLimits.MaxLabel, $"{path}.cot", problems);
        Optional(score.Model, ProjectLimits.MaxLabel, $"{path}.model", problems);
        Optional(score.EngineId, ProjectLimits.MaxLabel, $"{path}.engineId", problems);
        Optional(score.Label, ProjectLimits.MaxName, $"{path}.label", problems);
        Optional(score.Source, ProjectLimits.MaxLabel, $"{path}.source", problems);
        if (score.BudgetSeconds is { } budget)
        {
            problems.Range(budget, 0, ProjectLimits.MaxSeconds, $"{path}.budgetSeconds");
        }
        if (score.Created is < 0)
        {
            problems.Add($"{path}.created: a time since 1970 is not negative.");
        }
        problems.Range(score.Extra.Count, 0, MaxMetaFields, $"{path}.extra");
        CheckLink(score.Parent, clipId, $"{path}.parent", audioClipIds, problems);
    }

    private static void CheckLyrics(ClipLyrics lyrics, string path, ProblemList problems)
    {
        problems.Text(lyrics.Text, ProjectLimits.MaxLongText, $"{path}.text", required: false);
        Optional(lyrics.Source, ProjectLimits.MaxLabel, $"{path}.source", problems);
        Optional(lyrics.Model, ProjectLimits.MaxLabel, $"{path}.model", problems);
        problems.Range(lyrics.Extra.Count, 0, MaxMetaFields, $"{path}.extra");
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

    private static void Optional(string? value, int max, string path, ProblemList problems)
    {
        if (value is not null)
        {
            problems.Text(value, max, path, required: false);
        }
    }
}
