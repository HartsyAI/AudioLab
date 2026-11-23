using AudioLab.Core.Model;

namespace AudioLab.Audio.Instruments;

/// <summary>Works out, before a render, which notes of a track's MIDI clips sound and for how long, in timeline frames.</summary>
/// <remarks>
/// The old DAW's rules: a muted clip plays nothing and covers nothing; a later clip covers earlier ones, so a note that starts under it
/// does not sound and a note that is running when it starts is let go there (audit row 31); a note sounds no longer than its clip, and
/// a sustain pedal (controller 64, down from 64) that is down when the note ends holds it until the pedal comes up or the clip ends.
/// Note times are seconds, so no tempo enters here. It does not use the audio layering of <see cref="AudioLab.Core.Rules.LayerRules"/>, which
/// cuts spans with fades and seams; a note is covered by any later unmuted clip that reaches it. The work is quadratic in the clips of one
/// track that overlap, as the audio layering is.
/// </remarks>
internal static class NoteSchedule
{
    /// <summary>The quietest a note is played, so that a velocity of 1 is still heard.</summary>
    public const float QuietestVelocity = 0.05f;

    /// <summary>The shortest a note sounds before it is let go, in seconds, as the old DAW held it.</summary>
    public const double ShortestSounding = 0.01;

    private const int PedalController = 64;
    private const int PedalDownFrom = 64;
    private const double PedalTolerance = 1e-6;

    /// <summary>The notes that sound, ordered by start frame, then pitch, then their place in the track.</summary>
    public static NoteEvent[] Build(IReadOnlyList<MidiClip> clips, int sampleRate)
    {
        List<(NoteEvent Note, int Order)> notes = [];
        for (int i = 0; i < clips.Count; i++)
        {
            MidiClip clip = clips[i];
            if (clip.Muted)
            {
                continue;
            }
            List<(double Start, double End)> above = Later(clips, i);
            PedalEvent[] pedal = PedalOf(clip);
            foreach (MidiNote note in clip.Notes)
            {
                if (NoteEventOf(clip, note, above, pedal, sampleRate) is { } sounding)
                {
                    notes.Add((sounding, notes.Count));
                }
            }
        }
        notes.Sort((a, b) => Compare(a, b));
        return [.. notes.Select(entry => entry.Note)];
    }

    // The unmuted clips after clip i that reach into it, which are the only ones that can cover or cut its notes.
    private static List<(double Start, double End)> Later(IReadOnlyList<MidiClip> clips, int index)
    {
        MidiClip clip = clips[index];
        double end = clip.Start + clip.Length;
        List<(double Start, double End)> later = [];
        for (int j = index + 1; j < clips.Count; j++)
        {
            MidiClip other = clips[j];
            if (!other.Muted && other.Start < end && other.Start + other.Length > clip.Start)
            {
                later.Add((other.Start, other.Start + other.Length));
            }
        }
        return later;
    }

    private static NoteEvent? NoteEventOf(
        MidiClip clip,
        MidiNote note,
        List<(double Start, double End)> above,
        PedalEvent[] pedal,
        int sampleRate)
    {
        if (note.Velocity <= 0 || !(note.Start >= 0 && note.Start < clip.Length))
        {
            return null;
        }
        double on = clip.Start + note.Start;
        double duration = Math.Max(ShortestSounding, Sustained(clip, note, pedal));
        foreach ((double start, double end) in above)
        {
            if (on >= start && on < end)
            {
                return null;
            }
            double gap = start - on;
            if (gap > 0 && gap < duration)
            {
                duration = gap;
            }
        }
        long onFrame = RenderPlan.Frames(on, sampleRate);
        long offFrame = Math.Max(onFrame + 1, RenderPlan.Frames(on + duration, sampleRate));
        return new NoteEvent(onFrame, offFrame, note.Pitch, Math.Max(QuietestVelocity, Math.Min(1f, note.Velocity / 127f)));
    }

    // The note's length inside its clip, with the pedal held at the moment it ends carrying it on to the pedal's release.
    private static double Sustained(MidiClip clip, MidiNote note, PedalEvent[] pedal)
    {
        double length = Math.Min(Math.Max(0, note.Duration), clip.Length - note.Start);
        if (pedal.Length == 0)
        {
            return length;
        }
        double off = note.Start + length;
        int held = LastAtOrBefore(pedal, off);
        if (held < 0 || !pedal[held].Down)
        {
            return length;
        }
        double up = clip.Length;
        for (int i = held + 1; i < pedal.Length; i++)
        {
            if (!pedal[i].Down)
            {
                up = Math.Min(pedal[i].Time, clip.Length);
                break;
            }
        }
        return Math.Max(length, up - note.Start);
    }

    // The index of the last pedal event at or just after (within a millionth of a second of) the time, or -1 when none is.
    private static int LastAtOrBefore(PedalEvent[] pedal, double time)
    {
        int low = 0;
        int high = pedal.Length;
        while (low < high)
        {
            int middle = (low + high) >>> 1;
            if (pedal[middle].Time <= time + PedalTolerance)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low - 1;
    }

    private static PedalEvent[] PedalOf(MidiClip clip) =>
        [.. clip.ControlChanges
            .Where(change => change.Controller == PedalController)
            .OrderBy(change => change.Time)
            .Select(change => new PedalEvent(change.Time, change.Value >= PedalDownFrom))];

    private static int Compare((NoteEvent Note, int Order) a, (NoteEvent Note, int Order) b)
    {
        int result = a.Note.OnFrame.CompareTo(b.Note.OnFrame);
        result = result != 0 ? result : a.Note.Pitch.CompareTo(b.Note.Pitch);
        return result != 0 ? result : a.Order.CompareTo(b.Order);
    }

    private readonly record struct PedalEvent(double Time, bool Down);
}
