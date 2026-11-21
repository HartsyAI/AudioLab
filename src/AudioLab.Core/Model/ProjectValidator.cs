namespace AudioLab.Core.Model;

/// <summary>The rules a version 4 document keeps: unique ids, references that resolve, ordered and finite times.</summary>
public static class ProjectValidator
{
    private const double Tolerance = 1e-9;

    private static readonly int[] _denominators = [1, 2, 4, 8, 16, 32];

    /// <summary>Every rule the document breaks, each with the path of the value; empty when it is valid.</summary>
    public static IReadOnlyList<string> Validate(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<string> problems = [];
        CheckHeader(document, problems);
        CheckTempo(document, problems);
        CheckMarkersAndSections(document, problems);
        CheckGroups(document, problems);
        CheckBeatPattern(document.BeatPattern, problems);
        CheckTracks(document, problems);
        return problems;
    }

    private static void CheckHeader(ProjectDocument document, List<string> problems)
    {
        if (document.Version != ProjectDocument.CurrentVersion)
        {
            problems.Add($"version: expected {ProjectDocument.CurrentVersion}, found {document.Version}.");
        }
        RequireId(document.Id.Value, "id", problems);
        RequireId(document.Owner.Value, "owner", problems);
        Range(document.MasterVolume, 0, double.MaxValue, "masterVolume", problems);
        if (document.TimeSignature.Numerator < 1 || !_denominators.Contains(document.TimeSignature.Denominator))
        {
            problems.Add("timeSignature: the numerator must be 1 or more and the denominator 1, 2, 4, 8, 16 or 32.");
        }
    }

    private static void CheckTempo(ProjectDocument document, List<string> problems)
    {
        Range(document.Bpm, double.Epsilon, 1000, "bpm", problems);
        HashSet<string> ids = [];
        double previous = 0;
        for (int i = 0; i < document.TempoMap.Count; i++)
        {
            TempoChange change = document.TempoMap[i];
            string path = $"tempoMap[{i}]";
            Unique(ids, change.Id.Value, $"{path}.id", problems);
            Range(change.Bpm, double.Epsilon, 1000, $"{path}.bpm", problems);
            if (!(change.Time > previous) || !double.IsFinite(change.Time))
            {
                problems.Add($"{path}.time: tempo changes must be finite, above zero and in increasing order.");
            }
            previous = change.Time;
        }
    }

    private static void CheckMarkersAndSections(ProjectDocument document, List<string> problems)
    {
        HashSet<string> markerIds = [];
        double previous = 0;
        for (int i = 0; i < document.Markers.Count; i++)
        {
            Marker marker = document.Markers[i];
            Unique(markerIds, marker.Id.Value, $"markers[{i}].id", problems);
            Range(marker.Time, previous, double.MaxValue, $"markers[{i}].time", problems);
            previous = marker.Time;
        }
        HashSet<string> sectionIds = [];
        double end = 0;
        for (int i = 0; i < document.Sections.Count; i++)
        {
            Section section = document.Sections[i];
            string path = $"sections[{i}]";
            Unique(sectionIds, section.Id.Value, $"{path}.id", problems);
            if (!(section.Start >= end - Tolerance))
            {
                problems.Add($"{path}.start: a section must not overlap the one before it.");
            }
            if (!(section.End - section.Start >= Section.MinLength - Tolerance))
            {
                problems.Add($"{path}.end: a section must be at least {Section.MinLength} seconds long.");
            }
            end = section.End;
        }
    }

    private static void CheckGroups(ProjectDocument document, List<string> problems)
    {
        HashSet<string> ids = [];
        for (int i = 0; i < document.Groups.Count; i++)
        {
            Unique(ids, document.Groups[i].Id.Value, $"groups[{i}].id", problems);
            Range(document.Groups[i].Volume, 0, double.MaxValue, $"groups[{i}].volume", problems);
        }
    }

    private static void CheckBeatPattern(BeatPattern pattern, List<string> problems)
    {
        if (pattern.Steps < 1)
        {
            problems.Add("beatPattern.steps: a pattern has at least one step.");
        }
        Range(pattern.Swing, 0, 1, "beatPattern.swing", problems);
        HashSet<string> ids = [];
        for (int i = 0; i < pattern.Lanes.Count; i++)
        {
            BeatLane lane = pattern.Lanes[i];
            string path = $"beatPattern.lanes[{i}]";
            Unique(ids, lane.Id.Value, $"{path}.id", problems);
            Range(lane.Gain, 0, double.MaxValue, $"{path}.gain", problems);
            if (lane.Steps.Count != pattern.Steps)
            {
                problems.Add($"{path}.steps: a lane has {lane.Steps.Count} steps but the pattern has {pattern.Steps}.");
            }
        }
    }

    private static void CheckTracks(ProjectDocument document, List<string> problems)
    {
        HashSet<string> trackIds = [];
        HashSet<string> clipIds = [];
        HashSet<string> effectIds = [];
        Dictionary<string, Track> tracks = [];
        for (int i = 0; i < document.Tracks.Count; i++)
        {
            if (Unique(trackIds, document.Tracks[i].Id.Value, $"tracks[{i}].id", problems))
            {
                tracks[document.Tracks[i].Id.Value] = document.Tracks[i];
            }
        }
        HashSet<string> groupIds = [.. document.Groups.Select(group => group.Id.Value)];
        for (int i = 0; i < document.Tracks.Count; i++)
        {
            Track track = document.Tracks[i];
            string path = $"tracks[{i}]";
            CheckTrack(track, path, problems);
            if (track.Group is { } group && !groupIds.Contains(group.Value))
            {
                problems.Add($"{path}.group: no group has the id {group.Value}.");
            }
            CheckSends(track, path, tracks, problems);
            foreach (AudioClip clip in track.Clips)
            {
                CheckAudioClip(clip, $"{path}.clips[{clip.Id.Value}]", clipIds, problems);
            }
            foreach (MidiClip clip in track.MidiClips)
            {
                CheckMidiClip(clip, $"{path}.midiClips[{clip.Id.Value}]", clipIds, problems);
            }
            foreach (Effect effect in track.Effects)
            {
                Unique(effectIds, effect.Id.Value, $"{path}.effects[{effect.Id.Value}].id", problems);
            }
            CheckEnvelope(track.Automation.Volume, 0, double.MaxValue, $"{path}.automation.volume", problems);
            CheckEnvelope(track.Automation.Pan, -1, 1, $"{path}.automation.pan", problems);
        }
    }

    private static void CheckTrack(Track track, string path, List<string> problems)
    {
        Range(track.Volume, 0, double.MaxValue, $"{path}.volume", problems);
        Range(track.Pan, -1, 1, $"{path}.pan", problems);
        bool single = track.Input.Channels == InputChannelMode.Single;
        if (single != track.Input.Channel.HasValue || track.Input.Channel < 1)
        {
            problems.Add($"{path}.input: a single channel needs its 1-based number; other modes have none.");
        }
        bool hasContent = track.Clips.Count > 0 || track.MidiClips.Count > 0 || track.Instrument is not null || track.Sends.Count > 0;
        if (track.Kind == TrackKind.Bus && hasContent)
        {
            problems.Add($"{path}: a bus has no clips, instrument or sends.");
        }
        if (track.Kind != TrackKind.Instrument && track.Instrument is not null)
        {
            problems.Add($"{path}.instrument: only an instrument track has an instrument.");
        }
    }

    private static void CheckSends(Track track, string path, Dictionary<string, Track> tracks, List<string> problems)
    {
        HashSet<string> targets = [];
        for (int i = 0; i < track.Sends.Count; i++)
        {
            Send send = track.Sends[i];
            string sendPath = $"{path}.sends[{i}]";
            Range(send.Level, 0, double.MaxValue, $"{sendPath}.level", problems);
            if (!tracks.TryGetValue(send.Bus.Value ?? string.Empty, out Track? bus) || bus.Kind != TrackKind.Bus)
            {
                problems.Add($"{sendPath}.bus: {send.Bus.Value} is not a bus track of this project.");
            }
            else if (!targets.Add(send.Bus.Value!))
            {
                problems.Add($"{sendPath}.bus: the track already sends to {send.Bus.Value}.");
            }
        }
    }

    private static void CheckAudioClip(AudioClip clip, string path, HashSet<string> clipIds, List<string> problems)
    {
        Unique(clipIds, clip.Id.Value, $"{path}.id", problems);
        RequireId(clip.Media.Value, $"{path}.media", problems);
        Range(clip.Start, 0, double.MaxValue, $"{path}.start", problems);
        Range(clip.Duration, 0, double.MaxValue, $"{path}.duration", problems);
        Range(clip.Offset, 0, double.MaxValue, $"{path}.offset", problems);
        Range(clip.TrimEnd, 0, double.MaxValue, $"{path}.trimEnd", problems);
        Range(clip.Gain, 0, double.MaxValue, $"{path}.gain", problems);
        Range(clip.FadeIn, 0, double.MaxValue, $"{path}.fadeIn", problems);
        Range(clip.FadeOut, 0, double.MaxValue, $"{path}.fadeOut", problems);
    }

    private static void CheckMidiClip(MidiClip clip, string path, HashSet<string> clipIds, List<string> problems)
    {
        Unique(clipIds, clip.Id.Value, $"{path}.id", problems);
        Range(clip.Start, 0, double.MaxValue, $"{path}.start", problems);
        Range(clip.Length, double.Epsilon, double.MaxValue, $"{path}.length", problems);
        HashSet<int> noteIds = [];
        foreach (MidiNote note in clip.Notes)
        {
            string notePath = $"{path}.notes[{note.Id.Value}]";
            if (note.Id.Value < 1 || !noteIds.Add(note.Id.Value))
            {
                problems.Add($"{notePath}.id: a note id is 1 or more and unique within its clip.");
            }
            Range(note.Pitch, 0, 127, $"{notePath}.pitch", problems);
            Range(note.Velocity, 0, 127, $"{notePath}.velocity", problems);
            Range(note.Start, 0, double.MaxValue, $"{notePath}.start", problems);
            Range(note.Duration, 0, double.MaxValue, $"{notePath}.duration", problems);
        }
        double previous = 0;
        for (int i = 0; i < clip.ControlChanges.Count; i++)
        {
            MidiControlChange change = clip.ControlChanges[i];
            Range(change.Time, previous, double.MaxValue, $"{path}.controlChanges[{i}].time", problems);
            Range(change.Controller, 0, 127, $"{path}.controlChanges[{i}].controller", problems);
            Range(change.Value, 0, 127, $"{path}.controlChanges[{i}].value", problems);
            previous = change.Time;
        }
    }

    private static void CheckEnvelope(
        IReadOnlyList<AutomationPoint> points,
        double min,
        double max,
        string path,
        List<string> problems)
    {
        double previous = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Range(points[i].Time, previous, double.MaxValue, $"{path}[{i}].time", problems);
            Range(points[i].Value, min, max, $"{path}[{i}].value", problems);
            previous = points[i].Time;
        }
    }

    private static bool Unique(HashSet<string> seen, string? id, string path, List<string> problems)
    {
        if (string.IsNullOrEmpty(id))
        {
            problems.Add($"{path}: an id must not be empty.");
            return false;
        }
        if (!seen.Add(id))
        {
            problems.Add($"{path}: the id {id} is used more than once.");
            return false;
        }
        return true;
    }

    private static void RequireId(string? id, string path, List<string> problems)
    {
        if (string.IsNullOrEmpty(id))
        {
            problems.Add($"{path}: must not be empty.");
        }
    }

    private static void Range(double value, double min, double max, string path, List<string> problems)
    {
        if (!(value >= min && value <= max))
        {
            string expected = (min == double.Epsilon, max == double.MaxValue) switch
            {
                (true, true) => "above 0",
                (true, false) => $"above 0 and at most {max}",
                (false, true) => $"at least {min}",
                _ => $"from {min} to {max}",
            };
            problems.Add($"{path}: {value} must be {expected}.");
        }
    }
}
