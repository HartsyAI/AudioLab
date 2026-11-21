using System.Globalization;

namespace AudioLab.Core.Model;

/// <summary>The rules a version 4 document keeps: unique ids, references that resolve, ranges, order, finite numbers.</summary>
public static class ProjectValidator
{
    private static readonly int[] _denominators = [1, 2, 4, 8, 16, 32];

    /// <summary>Every rule the document breaks, each with the path of the value; empty when it is valid.</summary>
    public static IReadOnlyList<string> Validate(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ProblemList problems = new();
        CheckHeader(document, problems);
        CheckTempo(document, problems);
        CheckMarkers(document, problems);
        CheckSections(document, problems);
        CheckGroups(document, problems);
        CheckBeatPattern(document.BeatPattern, problems);
        TrackValidator.Check(document, problems);
        return problems.ToList();
    }

    private static void CheckHeader(ProjectDocument document, ProblemList problems)
    {
        if (document.Version != ProjectDocument.CurrentVersion)
        {
            problems.Add($"version: expected {ProjectDocument.CurrentVersion}, found {document.Version}.");
        }
        problems.Id(document.Id.Value, "id");
        problems.Id(document.Owner.Value, "owner");
        problems.Text(document.Name, ProjectLimits.MaxName, "name");
        problems.Range(document.MasterVolume, 0, ProjectLimits.MaxGain, "masterVolume");
        bool denominator = _denominators.Contains(document.TimeSignature.Denominator);
        if (document.TimeSignature.Numerator is < 1 or > 64 || !denominator)
        {
            problems.Add("timeSignature: the numerator is 1 to 64 and the denominator 1, 2, 4, 8, 16 or 32.");
        }
        problems.Text(document.View.Grid, ProjectLimits.MaxLabel, "view.grid");
        problems.Defined(document.View.Ruler, "view.ruler");
        problems.Defined(document.View.EditMode, "view.editMode");
    }

    private static void CheckTempo(ProjectDocument document, ProblemList problems)
    {
        problems.Range(document.Bpm, ProjectLimits.MinBpm, ProjectLimits.MaxBpm, "bpm");
        HashSet<string> ids = [];
        double previous = 0;
        for (int i = 0; i < document.TempoMap.Count; i++)
        {
            TempoChange change = document.TempoMap[i];
            string path = $"tempoMap[{i}]";
            problems.Unique(ids, change.Id.Value, $"{path}.id");
            problems.Range(change.Bpm, ProjectLimits.MinBpm, ProjectLimits.MaxBpm, $"{path}.bpm");
            if (!(change.Time > previous && change.Time <= ProjectLimits.MaxSeconds))
            {
                problems.Add($"{path}.time: tempo changes are above zero, finite and in increasing order.");
            }
            previous = double.IsFinite(change.Time) ? Math.Max(previous, change.Time) : previous;
        }
    }

    private static void CheckMarkers(ProjectDocument document, ProblemList problems)
    {
        HashSet<string> ids = [];
        double previous = 0;
        for (int i = 0; i < document.Markers.Count; i++)
        {
            Marker marker = document.Markers[i];
            string path = $"markers[{i}]";
            problems.Unique(ids, marker.Id.Value, $"{path}.id");
            problems.Range(marker.Time, previous, ProjectLimits.MaxSeconds, $"{path}.time");
            problems.Text(marker.Name, ProjectLimits.MaxName, $"{path}.name");
            if (marker.Color is not null)
            {
                problems.Color(marker.Color, $"{path}.color");
            }
            previous = double.IsFinite(marker.Time) ? Math.Max(previous, marker.Time) : previous;
        }
    }

    private static void CheckSections(ProjectDocument document, ProblemList problems)
    {
        HashSet<string> ids = [];
        double end = 0;
        for (int i = 0; i < document.Sections.Count; i++)
        {
            Section section = document.Sections[i];
            string path = $"sections[{i}]";
            problems.Unique(ids, section.Id.Value, $"{path}.id");
            problems.Text(section.Name, ProjectLimits.MaxName, $"{path}.name");
            problems.Color(section.Color, $"{path}.color");
            problems.Range(section.Start, 0, ProjectLimits.MaxSeconds, $"{path}.start");
            problems.Range(section.End, 0, ProjectLimits.MaxSeconds, $"{path}.end");
            if (section.Start < end - ProjectLimits.Tolerance)
            {
                problems.Add($"{path}.start: a section must not overlap the one before it.");
            }
            if (!(section.End - section.Start >= Section.MinLength - ProjectLimits.Tolerance))
            {
                problems.Add($"{path}.end: a section must be at least {Section.MinLength.ToString(CultureInfo.InvariantCulture)} seconds long.");
            }
            end = double.IsFinite(section.End) ? section.End : end;
        }
    }

    private static void CheckGroups(ProjectDocument document, ProblemList problems)
    {
        HashSet<string> ids = [];
        for (int i = 0; i < document.Groups.Count; i++)
        {
            TrackGroup group = document.Groups[i];
            string path = $"groups[{i}]";
            problems.Unique(ids, group.Id.Value, $"{path}.id");
            problems.Text(group.Name, ProjectLimits.MaxName, $"{path}.name");
            problems.Color(group.Color, $"{path}.color");
            problems.Range(group.Volume, 0, ProjectLimits.MaxGain, $"{path}.volume");
        }
    }

    private static void CheckBeatPattern(BeatPattern pattern, ProblemList problems)
    {
        problems.Range(pattern.Steps, 1, ProjectLimits.MaxBeatSteps, "beatPattern.steps");
        problems.Range(pattern.Swing, 0, 1, "beatPattern.swing");
        problems.Range(pattern.Lanes.Count, 0, ProjectLimits.MaxBeatLanes, "beatPattern.lanes");
        HashSet<string> ids = [];
        for (int i = 0; i < pattern.Lanes.Count; i++)
        {
            BeatLane lane = pattern.Lanes[i];
            string path = $"beatPattern.lanes[{i}]";
            problems.Unique(ids, lane.Id.Value, $"{path}.id");
            problems.Text(lane.Name, ProjectLimits.MaxName, $"{path}.name", required: false);
            problems.Range(lane.Gain, 0, ProjectLimits.MaxGain, $"{path}.gain");
            if (lane.Media is { } media)
            {
                problems.Id(media.Value, $"{path}.media");
            }
            if (lane.Steps.Count != pattern.Steps)
            {
                problems.Add($"{path}.steps: a lane has {lane.Steps.Count} steps but the pattern has {pattern.Steps}.");
            }
        }
    }
}
