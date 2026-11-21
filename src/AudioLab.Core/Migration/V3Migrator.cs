using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>The project-level part of the migration: header, tempo, markers, view and the drum machine.</summary>
internal static class V3Migrator
{
    private const double DefaultBpm = 120;

    private const int DefaultSteps = 16;

    public static ProjectDocument Migrate(V3Project project, UserId owner, V3Context context)
    {
        List<V3Group> rawGroups = project.Groups ?? [];
        List<TrackGroup> groups = V3Sections.ConvertGroups(rawGroups, context);
        List<V3Track> rawTracks = project.Tracks ?? [];
        V3Tracks.Reserve(rawTracks, context);
        List<Track> tracks = V3Tracks.Convert(rawTracks, V3Sections.GroupIdsByOldId(rawGroups, groups), context);
        return new ProjectDocument
        {
            Version = ProjectDocument.CurrentVersion,
            Id = context.Ids.Next<ProjectId>(),
            Owner = owner,
            Name = string.IsNullOrWhiteSpace(project.ProjectName) ? "Untitled project" : project.ProjectName,
            Bpm = project.Bpm is { } bpm && bpm != 0 ? bpm : DefaultBpm,
            TempoMap = ConvertTempo(project.TempoMap ?? [], context),
            TimeSignature = ConvertSignature(project.TimeSignature),
            MasterVolume = project.MasterVolume ?? 1,
            MasterLimiter = project.MasterLimiterEnabled != false,
            Metronome = project.Metronome == true,
            Markers = ConvertMarkers(project.Markers ?? [], context),
            Sections = V3Sections.ConvertSections(project.Sections ?? [], context),
            Groups = groups,
            BeatPattern = ConvertBeats(project.BeatPattern, context),
            Tracks = tracks,
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
        List<(double Time, double Bpm)> kept = [];
        foreach (V3Tempo change in raw)
        {
            if (change.T is > 0 && change.Bpm is > 0)
            {
                kept.Add((change.T.Value, change.Bpm.Value));
            }
            else
            {
                context.Warn("A tempo change without a time above zero and a tempo above zero was dropped.");
            }
        }
        List<TempoChange> result = [];
        double previous = 0;
        foreach ((double time, double bpm) in kept.OrderBy(change => change.Time))
        {
            if (time <= previous)
            {
                context.Warn($"A second tempo change at {time} seconds was dropped.");
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
                context.Warn($"Marker {i + 1} was before the start and moved to 0.");
                time = 0;
            }
            string name = string.IsNullOrWhiteSpace(raw[i].Name) ? $"Marker {i + 1}" : raw[i].Name!;
            kept.Add((time, name, string.IsNullOrEmpty(raw[i].Color) ? null : raw[i].Color));
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
            context.Warn($"The edit mode '{project.EditMode}' is unknown; {editMode} was used, as the extension does.");
        }
        return new ProjectView
        {
            Ruler = ruler,
            EditMode = editMode,
            Grid = string.IsNullOrEmpty(project.GridValue) ? "auto" : project.GridValue,
        };
    }

    private static BeatPattern ConvertBeats(V3BeatPattern? raw, V3Context context)
    {
        int steps = raw?.Steps is { } count && count != 0 ? count : DefaultSteps;
        List<BeatLane> lanes = [];
        List<V3BeatLane> rawLanes = raw?.Lanes ?? [];
        for (int i = 0; i < rawLanes.Count; i++)
        {
            lanes.Add(new BeatLane
            {
                Id = context.Ids.Next<BeatLaneId>(),
                Name = rawLanes[i].Name ?? string.Empty,
                Media = string.IsNullOrEmpty(rawLanes[i].BlobKey) ? null : context.MediaFor(rawLanes[i].BlobKey!),
                Gain = rawLanes[i].Gain ?? 1,
                Steps = ConvertSteps(rawLanes[i].Steps ?? [], steps, i, context),
            });
        }
        return new BeatPattern { Steps = steps, Swing = raw?.Swing ?? 0, Lanes = lanes };
    }

    private static List<bool> ConvertSteps(List<JsonElement> raw, int steps, int lane, V3Context context)
    {
        if (raw.Skip(steps).Any(Truthy))
        {
            context.Warn($"Beat lane {lane + 1} had steps beyond the pattern length; they never played and were dropped.");
        }
        return [.. Enumerable.Range(0, Math.Max(steps, 0)).Select(i => i < raw.Count && Truthy(raw[i]))];
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
