using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>MIDI clips and automation points. Times outside their clip or lane are repaired, not refused.</summary>
internal static class V3Midi
{
    private const int DefaultVelocity = 100;

    /// <summary>A MIDI clip with fresh note ids; notes and controllers outside the clip are repaired or dropped.</summary>
    public static MidiClip ConvertClip(V3MidiClip raw, string path, V3Context context)
    {
        double length = Math.Max(MidiClip.MinLength, raw.Length ?? 0);
        List<MidiNote> notes = ConvertNotes(V3Context.Items(raw.Notes, $"{path}.notes"), length, path, context);
        return new MidiClip
        {
            Id = context.Ids.Next<ClipId>(),
            Name = raw.Name ?? string.Empty,
            Start = raw.StartTime ?? 0,
            Length = length,
            NextNoteId = new NoteId(notes.Count).Next(),
            Notes = [.. notes],
            ControlChanges = [.. ConvertControllers(V3Context.Items(raw.Cc, $"{path}.cc"), length, path, context)],
            Muted = raw.Muted == true,
            Color = context.Color(raw.Color, null, $"{path}.color"),
        };
    }

    /// <summary>An automation lane in time order; points before the start move to 0.</summary>
    public static List<AutomationPoint> ConvertPoints(List<V3Point>? raw, string path, V3Context context)
    {
        List<AutomationPoint> points = [];
        List<V3Point> source = V3Context.Items(raw, path);
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
    // A note that starts at or after the clip end never plays, so it is dropped.
    private static List<MidiNote> ConvertNotes(List<V3Note> raw, double length, string path, V3Context context)
    {
        List<MidiNote> notes = [];
        NoteId next = NoteId.First;
        int trimmed = 0;
        int beyond = 0;
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
            }
            if (duration <= 0)
            {
                continue;
            }
            if (start >= length)
            {
                beyond++;
                continue;
            }
            notes.Add(new MidiNote(next, pitch, start, duration, velocity));
            next = next.Next();
        }
        Report(context, path, trimmed, "notes starting before the clip; the part inside the clip was kept");
        Report(context, path, beyond, "notes starting after the clip end; they never played and were dropped");
        return notes;
    }

    private static List<MidiControlChange> ConvertControllers(
        List<V3Cc> raw,
        double length,
        string path,
        V3Context context)
    {
        List<MidiControlChange> changes = [];
        int moved = 0;
        int beyond = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            string changePath = $"{path}.cc[{i}]";
            double time = raw[i].T ?? throw V3Context.Fail($"{changePath}.t", "is missing.");
            int controller = Whole(raw[i].N, $"{changePath}.n", context);
            int value = Whole(raw[i].V, $"{changePath}.v", context);
            moved += time < 0 ? 1 : 0;
            if (time > length)
            {
                beyond++;
                continue;
            }
            changes.Add(new MidiControlChange(Math.Max(0, time), controller, value));
        }
        Report(context, path, moved, "controller events before the clip; they were moved to its start");
        Report(context, path, beyond, "controller events after the clip end; they never took effect and were dropped");
        return [.. changes.OrderBy(change => change.Time)];
    }

    private static void Report(V3Context context, string path, int count, string what)
    {
        if (count > 0)
        {
            context.Warn($"{path} had {count} {what}.");
        }
    }

    private static int Whole(double? value, string path, V3Context context)
    {
        double number = value ?? throw V3Context.Fail(path, "is missing.");
        // JavaScript's Math.round, so 60.5 is 61 and not the even neighbour.
        double rounded = Math.Clamp(Math.Floor(number + 0.5), int.MinValue, int.MaxValue);
        if (rounded != number)
        {
            context.Warn($"{path} was {V3Context.Num(number)} and was rounded to {V3Context.Num(rounded)}.");
        }
        return (int)rounded;
    }
}
