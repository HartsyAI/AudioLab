using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The automation commands: the volume and pan lanes of a track, edited as the old DAW's lane editor edits them.</summary>
internal static class AutomationCommands
{
    public static void Register(CommandRegistryBuilder builder, AutomationCommandJsonContext json)
    {
        builder
            .Add("automation_point_add", "Adds a point to a volume or pan lane.", json.AddAutomationPoint, AddPoint)
            .Add("automation_point_move", "Puts a point at a new time and value.", json.MoveAutomationPoint, MovePoint)
            .Add("automation_point_delete", "Removes a point from a lane.", json.DeleteAutomationPoint, DeletePoint)
            .Add("automation_points_set_values", "Sets the values of points and leaves their times.", json.SetAutomationValues, SetValues)
            .Add("automation_write_commit", "Folds a recorded automation pass into a lane.", json.CommitAutomationWrite, CommitWrite)
            .Add("automation_set_mode", "Sets how a track's lanes behave during playback.", json.SetAutomationMode, SetMode)
            .Add("automation_set_view", "Shows or hides a track's lane and chooses the lane it shows.", json.SetAutomationView, SetView);
    }

    private static CommandResult AddPoint(ProjectDocument document, AddAutomationPoint command, IIdService ids)
    {
        if (TrackEdits.Exists(document, command.Track) is { } missing)
        {
            return missing;
        }
        CommandResult? invalid = CommandChecks.Range(nameof(command.Time), command.Time, 0, ProjectLimits.MaxSeconds)
            ?? Value(command.Parameter, command.Value);
        if (invalid is not null)
        {
            return invalid;
        }
        ValueList<AutomationPoint> lane = LaneOf(document, command.Track, command.Parameter);
        return lane.Count >= ProjectLimits.MaxListItems
            ? TooMany()
            : Replace(
                document,
                command.Track,
                command.Parameter,
                AutomationRules.Insert(lane, new AutomationPoint(command.Time, command.Value)).Points);
    }

    private static CommandResult MovePoint(ProjectDocument document, MoveAutomationPoint command, IIdService ids)
    {
        if (Find(document, command.Track, command.Parameter, [command.Index]) is { } missing)
        {
            return missing;
        }
        CommandResult? invalid = CommandChecks.Range(nameof(command.Time), command.Time, 0, ProjectLimits.MaxSeconds)
            ?? CommandChecks.Range(nameof(command.Value), command.Value, -1, AutomationRules.VolumeMax);
        if (invalid is not null)
        {
            return invalid;
        }
        double value = AutomationRules.Clamp(command.Parameter, command.Value);
        ValueList<AutomationPoint> lane = LaneOf(document, command.Track, command.Parameter);
        LaneEdit moved = AutomationRules.Move(lane, command.Index, new AutomationPoint(command.Time, value));
        return Replace(document, command.Track, command.Parameter, moved.Points);
    }

    private static CommandResult DeletePoint(ProjectDocument document, DeleteAutomationPoint command, IIdService ids)
    {
        if (Find(document, command.Track, command.Parameter, [command.Index]) is { } missing)
        {
            return missing;
        }
        ValueList<AutomationPoint> lane = LaneOf(document, command.Track, command.Parameter);
        return Replace(document, command.Track, command.Parameter, [.. lane.Where((_, index) => index != command.Index)]);
    }

    private static CommandResult SetValues(ProjectDocument document, SetAutomationValues command, IIdService ids)
    {
        if (TrackEdits.Exists(document, command.Track) is { } missing)
        {
            return missing;
        }
        if (command.Points.Count == 0 || command.Points.Count > ProjectLimits.MaxListItems)
        {
            return CommandChecks.OutOfRange($"Name from 1 to {ProjectLimits.MaxListItems} points.");
        }
        if (Find(document, command.Track, command.Parameter, [.. command.Points.Select(point => point.Index)]) is { } unknown)
        {
            return unknown;
        }
        Dictionary<int, double> values = [];
        foreach (PointValue point in command.Points)
        {
            if (CommandChecks.Range("value", point.Value, -1, AutomationRules.VolumeMax) is { } invalid)
            {
                return invalid;
            }
            values[point.Index] = AutomationRules.Clamp(command.Parameter, point.Value);
        }
        ValueList<AutomationPoint> lane = LaneOf(document, command.Track, command.Parameter);
        return Replace(
            document,
            command.Track,
            command.Parameter,
            [.. lane.Select((point, index) => values.TryGetValue(index, out double value) ? point with { Value = value } : point)]);
    }

    private static CommandResult CommitWrite(ProjectDocument document, CommitAutomationWrite command, IIdService ids)
    {
        if (TrackEdits.Exists(document, command.Track) is { } missing)
        {
            return missing;
        }
        if (command.Points.Count == 0 || command.Points.Count > ProjectLimits.MaxListItems)
        {
            return CommandChecks.OutOfRange($"Name from 1 to {ProjectLimits.MaxListItems} points.");
        }
        foreach (WrittenPoint point in command.Points)
        {
            CommandResult? invalid = CommandChecks.Range("time", point.Time, 0, ProjectLimits.MaxSeconds) ?? Value(command.Parameter, point.Value);
            if (invalid is not null)
            {
                return invalid;
            }
        }
        List<AutomationPoint> written = [.. command.Points.Select(point => new AutomationPoint(point.Time, point.Value))];
        ValueList<AutomationPoint> lane = LaneOf(document, command.Track, command.Parameter);
        ValueList<AutomationPoint> merged = AutomationRules.Commit(lane, written, command.Parameter, command.ReturnToOld);
        return merged.Count > ProjectLimits.MaxListItems ? TooMany() : Replace(document, command.Track, command.Parameter, merged);
    }

    private static CommandResult SetMode(ProjectDocument document, SetAutomationMode command, IIdService ids)
    {
        return TrackEdits.Replace(document, command.Track, track => track with { Automation = track.Automation with { Mode = command.Mode } });
    }

    private static CommandResult SetView(ProjectDocument document, SetAutomationView command, IIdService ids)
    {
        if (TrackEdits.Exists(document, command.Track) is { } missing)
        {
            return missing;
        }
        if (command.Visible is null && command.Parameter is null)
        {
            return CommandChecks.OutOfRange("Give visible, parameter or both.");
        }
        return TrackEdits.Replace(document, command.Track, track => track with
        {
            View = track.View with
            {
                AutomationVisible = command.Visible ?? track.View.AutomationVisible,
                AutomationParameter = command.Parameter ?? track.View.AutomationParameter,
            },
        });
    }

    // A value outside the lane is out of range for an add or a recorded pass; a drag is kept inside the lane instead.
    private static CommandResult? Value(AutomationParameter parameter, double value)
    {
        return CommandChecks.Range(nameof(value), value, AutomationRules.Min(parameter), AutomationRules.Max(parameter));
    }

    // A refusal when the track is missing or an index names no point of the lane; null when every index names one.
    private static CommandResult? Find(ProjectDocument document, TrackId track, AutomationParameter parameter, List<int> indexes)
    {
        if (TrackEdits.Exists(document, track) is { } missing)
        {
            return missing;
        }
        int count = LaneOf(document, track, parameter).Count;
        foreach (int index in indexes)
        {
            if (index < 0 || index >= count)
            {
                return CommandChecks.NotFound("automation point", index.ToString(CultureInfo.InvariantCulture));
            }
        }
        return null;
    }

    private static ValueList<AutomationPoint> LaneOf(ProjectDocument document, TrackId track, AutomationParameter parameter)
    {
        return AutomationRules.Lane(document.Tracks.First(candidate => candidate.Id == track).Automation, parameter);
    }

    private static CommandResult TooMany()
    {
        return CommandChecks.OutOfRange($"A lane holds as many points as a lane may, {ProjectLimits.MaxListItems}.");
    }

    private static CommandResult Replace(ProjectDocument document, TrackId track, AutomationParameter parameter, ValueList<AutomationPoint> points)
    {
        return TrackEdits.Replace(
            document,
            track,
            current => current with { Automation = AutomationRules.WithLane(current.Automation, parameter, points) });
    }
}
