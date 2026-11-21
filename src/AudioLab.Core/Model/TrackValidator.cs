namespace AudioLab.Core.Model;

/// <summary>The track rules: what each kind holds, sends, groups, and the ids all clips and effects share.</summary>
internal static class TrackValidator
{
    public static void Check(ProjectDocument document, ProblemList problems)
    {
        problems.Range(document.Tracks.Count, 0, ProjectLimits.MaxTracks, "tracks");
        HashSet<string> trackIds = [];
        Dictionary<string, Track> tracks = [];
        for (int i = 0; i < document.Tracks.Count; i++)
        {
            if (problems.Unique(trackIds, document.Tracks[i].Id.Value, $"tracks[{i}].id"))
            {
                tracks[document.Tracks[i].Id.Value] = document.Tracks[i];
            }
        }
        HashSet<string> groupIds = [.. document.Groups.Select(group => group.Id.Value)];
        HashSet<string> audioClipIds =
            [.. document.Tracks.SelectMany(track => track.Clips).Select(clip => clip.Id.Value)];
        HashSet<string> clipIds = [];
        HashSet<string> effectIds = [];
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
                ClipValidator.CheckAudioClip(clip, $"{path}.clips[{clip.Id.Value}]", clipIds, audioClipIds, problems);
            }
            foreach (MidiClip clip in track.MidiClips)
            {
                ClipValidator.CheckMidiClip(clip, $"{path}.midiClips[{clip.Id.Value}]", clipIds, problems);
            }
            SoundValidator.CheckEffects(track.Effects, path, effectIds, problems);
            CheckEnvelope(track.Automation.Volume, 0, ProjectLimits.MaxGain, $"{path}.automation.volume", problems);
            CheckEnvelope(track.Automation.Pan, -1, 1, $"{path}.automation.pan", problems);
        }
    }

    private static void CheckTrack(Track track, string path, ProblemList problems)
    {
        problems.Text(track.Name, ProjectLimits.MaxName, $"{path}.name");
        problems.Color(track.Color, $"{path}.color");
        problems.Range(track.Volume, 0, ProjectLimits.MaxGain, $"{path}.volume");
        problems.Range(track.Pan, -1, 1, $"{path}.pan");
        problems.Range(track.TakeCount, 0, 1_000_000, $"{path}.takeCount");
        problems.Range(track.View.Height, 16, 2000, $"{path}.view.height");
        problems.Defined(track.Kind, $"{path}.kind");
        problems.Defined(track.Automation.Mode, $"{path}.automation.mode");
        problems.Defined(track.View.AutomationParameter, $"{path}.view.automationParameter");
        if (track.Icon is { } icon && !TrackIcon.IsValid(icon))
        {
            problems.Add($"{path}.icon: an icon is short text or a small png, jpeg or webp image.");
        }
        CheckInput(track.Input, $"{path}.input", problems);
        CheckKind(track, path, problems);
        if (track.Instrument is not null)
        {
            SoundValidator.CheckInstrument(track.Instrument, $"{path}.instrument", problems);
        }
    }

    private static void CheckInput(TrackInput input, string path, ProblemList problems)
    {
        problems.Defined(input.Channels, $"{path}.channels");
        if (input.DeviceId is { } device)
        {
            problems.Text(device, 512, $"{path}.deviceId");
        }
        bool single = input.Channels == InputChannelMode.Single;
        if (single != input.Channel.HasValue || (single && input.Channel is < 1 or > 256))
        {
            problems.Add($"{path}: a single channel needs its number, 1 to 256; the other modes have none.");
        }
    }

    // An audio track holds audio clips, an instrument track holds an instrument and MIDI clips, a bus holds neither.
    private static void CheckKind(Track track, string path, ProblemList problems)
    {
        switch (track.Kind)
        {
            case TrackKind.Audio:
                if (track.MidiClips.Count > 0 || track.Instrument is not null)
                {
                    problems.Add($"{path}: an audio track has no MIDI clips or instrument.");
                }
                break;
            case TrackKind.Instrument:
                if (track.Clips.Count > 0)
                {
                    problems.Add($"{path}: an instrument track has no audio clips.");
                }
                if (track.Instrument is null)
                {
                    problems.Add($"{path}.instrument: an instrument track needs an instrument.");
                }
                break;
            case TrackKind.Bus:
                bool holdsAnything = track.Clips.Count > 0 || track.MidiClips.Count > 0
                    || track.Instrument is not null || track.Sends.Count > 0;
                if (holdsAnything)
                {
                    problems.Add($"{path}: a bus has no clips, instrument or sends.");
                }
                break;
        }
    }

    private static void CheckSends(Track track, string path, Dictionary<string, Track> tracks, ProblemList problems)
    {
        HashSet<string> targets = [];
        for (int i = 0; i < track.Sends.Count; i++)
        {
            Send send = track.Sends[i];
            string sendPath = $"{path}.sends[{i}]";
            problems.Range(send.Level, 0, ProjectLimits.MaxGain, $"{sendPath}.level");
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

    private static void CheckEnvelope(
        ValueList<AutomationPoint> points,
        double min,
        double max,
        string path,
        ProblemList problems)
    {
        double previous = 0;
        for (int i = 0; i < points.Count; i++)
        {
            problems.Range(points[i].Time, previous, ProjectLimits.MaxSeconds, $"{path}[{i}].time");
            problems.Range(points[i].Value, min, max, $"{path}[{i}].value");
            previous = double.IsFinite(points[i].Time) ? Math.Max(previous, points[i].Time) : previous;
        }
    }
}
