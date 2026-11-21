using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>MIDI clips and automation points. Times before the start of their clip or lane are repaired, not refused.</summary>
internal static class V3Midi
{
    private const double MinLength = 0.05;

    private const int DefaultVelocity = 100;

    public static MidiClip ConvertClip(V3MidiClip raw, string path, V3Context context)
    {
        return new MidiClip
        {
            Id = context.Ids.Next<ClipId>(),
            Name = raw.Name ?? string.Empty,
            Start = raw.StartTime ?? 0,
            Length = Math.Max(MinLength, raw.Length ?? 0),
            Notes = ConvertNotes(raw.Notes ?? [], path, context),
            ControlChanges = ConvertControllers(raw.Cc ?? [], path, context),
            Muted = raw.Muted == true,
            Color = string.IsNullOrEmpty(raw.Color) ? null : raw.Color,
        };
    }

    public static List<AutomationPoint> ConvertPoints(List<V3Point>? raw, string path, V3Context context)
    {
        List<AutomationPoint> points = [];
        List<V3Point> source = raw ?? [];
        int moved = 0;
        for (int i = 0; i < source.Count; i++)
        {
            double time = source[i].T ?? throw V3Context.Fail($"{path}[{i}].t", "is missing.");
            double value = source[i].V ?? throw V3Context.Fail($"{path}[{i}].v", "is missing.");
            moved += time < 0 ? 1 : 0;
            points.Add(new AutomationPoint(Math.Max(0, time), value));
        }
        if (moved > 0)
        {
            context.Warn($"{path} had {moved} points before the start; they were moved to 0.");
        }
        return [.. points.OrderBy(point => point.Time)];
    }

    // A shuffle delete once saved notes with negative times (audit row 65): the part inside the clip is kept.
    private static List<MidiNote> ConvertNotes(List<V3Note> raw, string path, V3Context context)
    {
        List<MidiNote> notes = [];
        NoteId next = NoteId.First;
        int trimmed = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            string notePath = $"{path}.notes[{i}]";
            int pitch = Whole(raw[i].Pitch, $"{notePath}.pitch", context);
            double start = raw[i].Start ?? throw V3Context.Fail($"{notePath}.start", "is missing.");
            double duration = raw[i].Dur ?? throw V3Context.Fail($"{notePath}.dur", "is missing.");
            int velocity = raw[i].Vel is null ? DefaultVelocity : Whole(raw[i].Vel, $"{notePath}.vel", context);
            if (start < 0)
            {
                trimmed++;
                duration += start;
                start = 0;
                if (duration <= 0)
                {
                    continue;
                }
            }
            notes.Add(new MidiNote(next, pitch, start, duration, velocity));
            next = next.Next();
        }
        if (trimmed > 0)
        {
            context.Warn($"{path} had {trimmed} notes starting before the clip; the part inside the clip was kept.");
        }
        return notes;
    }

    private static List<MidiControlChange> ConvertControllers(List<V3Cc> raw, string path, V3Context context)
    {
        List<MidiControlChange> changes = [];
        int moved = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            string changePath = $"{path}.cc[{i}]";
            double time = raw[i].T ?? throw V3Context.Fail($"{changePath}.t", "is missing.");
            moved += time < 0 ? 1 : 0;
            changes.Add(new MidiControlChange(
                Math.Max(0, time),
                Whole(raw[i].N, $"{changePath}.n", context),
                Whole(raw[i].V, $"{changePath}.v", context)));
        }
        if (moved > 0)
        {
            context.Warn($"{path} had {moved} controller events before the clip; they were moved to its start.");
        }
        return [.. changes.OrderBy(change => change.Time)];
    }

    private static int Whole(double? value, string path, V3Context context)
    {
        double number = value ?? throw V3Context.Fail(path, "is missing.");
        double rounded = Math.Clamp(Math.Round(number), int.MinValue, int.MaxValue);
        if (rounded != number)
        {
            context.Warn($"{path} was {number} and was rounded to {rounded}.");
        }
        return (int)rounded;
    }
}
