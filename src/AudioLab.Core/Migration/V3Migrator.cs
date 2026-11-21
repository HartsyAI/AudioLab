using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>The project-level part of the migration: header, tempo, markers, view and the drum machine.</summary>
internal static class V3Migrator
{
    private const double DefaultBpm = 120;

    private const int DefaultSteps = 16;

    /// <summary>Converts a version 3 project into a version 4 document, recording each change in the context.</summary>
    public static ProjectDocument Migrate(V3Project project, UserId owner, V3Context context)
    {
        List<V3Group> rawGroups = V3Context.Items(project.Groups, "groups");
        List<TrackGroup> groups = V3Sections.ConvertGroups(rawGroups, context);
        List<V3Track> rawTracks = V3Context.Items(project.Tracks, "tracks");
        V3Tracks.Reserve(rawTracks, context);
        List<Track> tracks = V3Tracks.Convert(rawTracks, V3Sections.GroupIdsByOldId(rawGroups, groups), context);
        tracks = V3Tracks.BreakScoreLinkLoops(tracks, context);
        return new ProjectDocument
        {
            Version = ProjectDocument.CurrentVersion,
            Id = context.Ids.Next<ProjectId>(),
            Owner = owner,
            Name = string.IsNullOrWhiteSpace(project.ProjectName) ? "Untitled project" : project.ProjectName,
            Bpm = project.Bpm is { } bpm && bpm != 0 ? bpm : DefaultBpm,
            TempoMap = [.. ConvertTempo(V3Context.Items(project.TempoMap, "tempoMap"), context)],
            TimeSignature = ConvertSignature(project.TimeSignature),
            MasterVolume = project.MasterVolume ?? 1,
            MasterLimiter = project.MasterLimiterEnabled != false,
            Metronome = project.Metronome == true,
            Markers = [.. ConvertMarkers(V3Context.Items(project.Markers, "markers"), context)],
            Sections = [.. V3Sections.ConvertSections(V3Context.Items(project.Sections, "sections"), context)],
            Groups = [.. groups],
            BeatPattern = ConvertBeats(project.BeatPattern, context),
            Tracks = [.. tracks],
            View = ConvertView(project, context),
        };
    }

    private static TimeSignature ConvertSignature(List<int>? raw)
    {
        if (raw is null)
        {
            return new TimeSignature(4, 4);
        }
        return raw.Count == 2
            ? new TimeSignature(raw[0], raw[1])
            : throw V3Context.Fail("timeSignature", "must be a pair such as [4, 4].");
    }

    private static List<TempoChange> ConvertTempo(List<V3Tempo> raw, V3Context context)
    {
        List<(int Index, double Time, double Bpm)> kept = [];
        for (int i = 0; i < raw.Count; i++)
        {
            if (raw[i].T is > 0 && raw[i].Bpm is > 0)
            {
                kept.Add((i, raw[i].T!.Value, raw[i].Bpm!.Value));
            }
            else
            {
                context.Warn($"tempoMap[{i}] needs a time above 0 and a tempo above 0; it was dropped.");
            }
        }
        List<TempoChange> result = [];
        double previous = 0;
        foreach ((int index, double time, double bpm) in kept.OrderBy(change => change.Time))
        {
            if (time <= previous)
            {
                context.Warn($"tempoMap[{index}] is a second change at {V3Context.Num(time)} seconds; it was dropped.");
                continue;
            }
            result.Add(new TempoChange(context.Ids.Next<TempoChangeId>(), time, bpm));
            previous = time;
        }
        return result;
    }

    private static List<Marker> ConvertMarkers(List<V3Marker> raw, V3Context context)
    {
        List<(double Time, string Name, string? Color)> kept = [];
        for (int i = 0; i < raw.Count; i++)
        {
            double time = raw[i].Time ?? throw V3Context.Fail($"markers[{i}].time", "is missing.");
            if (time < 0)
            {
                context.Warn($"markers[{i}] was before the start and moved to 0.");
                time = 0;
            }
            string name = string.IsNullOrWhiteSpace(raw[i].Name) ? $"Marker {i + 1}" : raw[i].Name!;
            kept.Add((time, name, context.Color(raw[i].Color, null, $"markers[{i}].color")));
        }
        return
        [
            .. kept.OrderBy(marker => marker.Time).Select(marker => new Marker
            {
                Id = context.Ids.Next<MarkerId>(),
                Time = marker.Time,
                Name = marker.Name,
                Color = marker.Color,
            }),
        ];
    }

    private static ProjectView ConvertView(V3Project project, V3Context context)
    {
        RulerMode ruler = project.RulerMode switch
        {
            null or "time" => RulerMode.Time,
            "beats" => RulerMode.Beats,
            _ => throw V3Context.Fail("rulerMode", $"'{project.RulerMode}' is not time or beats."),
        };
        EditMode editMode = project.EditMode switch
        {
            "slip" => EditMode.Slip,
            "grid" => EditMode.Grid,
            "shuffle" => EditMode.Shuffle,
            _ => project.SnapEnabled == false ? EditMode.Slip : EditMode.Grid,
        };
        if (project.EditMode is not (null or "slip" or "grid" or "shuffle"))
        {
            context.Warn($"editMode '{project.EditMode}' is unknown; {editMode} was used, as the extension does.");
        }
        return new ProjectView
        {
            Ruler = ruler,
            EditMode = editMode,
            Grid = string.IsNullOrEmpty(project.GridValue) ? "auto" : project.GridValue,
        };
    }

    // The step count is checked before anything is sized by it: the extension offers 16 or 32.
    private static BeatPattern ConvertBeats(V3BeatPattern? raw, V3Context context)
    {
        int steps = raw?.Steps is { } count && count != 0 ? count : DefaultSteps;
        if (steps < 1 || steps > ProjectLimits.MaxBeatSteps)
        {
            throw V3Context.Fail("beatPattern.steps", $"{steps} is not from 1 to {ProjectLimits.MaxBeatSteps}.");
        }
        List<V3BeatLane> rawLanes = V3Context.Items(raw?.Lanes, "beatPattern.lanes");
        if (rawLanes.Count > ProjectLimits.MaxBeatLanes)
        {
            string limit = $"{rawLanes.Count} lanes is more than {ProjectLimits.MaxBeatLanes}.";
            throw V3Context.Fail("beatPattern.lanes", limit);
        }
        List<BeatLane> lanes = [];
        for (int i = 0; i < rawLanes.Count; i++)
        {
            string path = $"beatPattern.lanes[{i}]";
            lanes.Add(new BeatLane
            {
                Id = context.Ids.Next<BeatLaneId>(),
                Name = rawLanes[i].Name ?? string.Empty,
                Media = LaneMedia(rawLanes[i].BlobKey, path, context),
                Gain = rawLanes[i].Gain ?? 1,
                Steps = [.. ConvertSteps(rawLanes[i].Steps ?? [], steps, path, context)],
            });
        }
        return new BeatPattern { Steps = steps, Swing = raw?.Swing ?? 0, Lanes = [.. lanes] };
    }

    private static MediaId? LaneMedia(string? blobKey, string path, V3Context context)
    {
        if (string.IsNullOrEmpty(blobKey))
        {
            return null;
        }
        MediaId? media = context.MediaFor(blobKey, $"{path}.blobKey");
        if (media is null)
        {
            context.Warn($"{path}: its audio '{blobKey}' is not among the audio files; the lane has no sample.");
        }
        return media;
    }

    private static List<bool> ConvertSteps(List<JsonElement> raw, int steps, string path, V3Context context)
    {
        if (raw.Skip(steps).Any(Truthy))
        {
            context.Warn($"{path} had steps beyond the pattern length; they never played and were dropped.");
        }
        return [.. Enumerable.Range(0, steps).Select(i => i < raw.Count && Truthy(raw[i]))];
    }

    // The extension reads a step with !!, so true, a non-zero number or a non-empty string is on.
    private static bool Truthy(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.GetDouble() != 0,
            JsonValueKind.String => value.GetString()!.Length > 0,
            _ => false,
        };
    }
}
