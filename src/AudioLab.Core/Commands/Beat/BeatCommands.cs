using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The drum pattern commands: its lanes of pads, their steps, the step count and the swing.</summary>
internal static class BeatCommands
{
    public static void Register(CommandRegistryBuilder builder, BeatCommandJsonContext json)
    {
        builder
            .Add("beat_lane_add", "Adds a lane to the drum pattern and allocates its id.", json.AddBeatLane, AddLane)
            .Add("beat_lane_remove", "Removes a lane from the drum pattern.", json.RemoveBeatLane, RemoveLane)
            .Add("beat_lane_move", "Moves a drum lane one place up or down in the pattern.", json.MoveBeatLane, MoveLane)
            .Add("beat_lane_rename", "Renames a drum lane.", json.RenameBeatLane, RenameLane)
            .Add("beat_lane_set_gain", "Sets the gain of a drum lane.", json.SetBeatLaneGain, SetGain)
            .Add("beat_lane_set_media", "Sets the sample a drum lane plays.", json.SetBeatLaneMedia, SetMedia)
            .Add("beat_lane_set_steps", "Switches steps of a drum lane on or off.", json.SetBeatLaneSteps, SetLaneSteps)
            .Add("beat_pattern_set_steps", "Sets how many steps the pattern has.", json.SetBeatSteps, SetSteps)
            .Add("beat_pattern_set_swing", "Sets the swing of the pattern.", json.SetBeatSwing, SetSwing);
    }

    private static CommandResult AddLane(ProjectDocument document, AddBeatLane command, IIdService ids)
    {
        BeatPattern pattern = document.BeatPattern;
        if (CommandChecks.TooLong(command.Name) is { } tooLong)
        {
            return tooLong;
        }
        if (pattern.Lanes.Count >= ProjectLimits.MaxBeatLanes)
        {
            return CommandChecks.OutOfRange($"A pattern has at most {ProjectLimits.MaxBeatLanes} lanes.");
        }
        BeatLane lane = new()
        {
            Id = ids.Next<BeatLaneId>(),
            Name = string.IsNullOrWhiteSpace(command.Name) ? $"Pad {pattern.Lanes.Count + 1}" : command.Name,
            Media = command.Media,
            Gain = BeatRules.DefaultGain,
            Steps = BeatRules.Resize([], pattern.Steps),
        };
        return CommandResult.Ok(document with { BeatPattern = pattern with { Lanes = [.. pattern.Lanes, lane] } }, lane.Id.Value);
    }

    private static CommandResult RemoveLane(ProjectDocument document, RemoveBeatLane command, IIdService ids)
    {
        return Edit(document, command.Lane, lanes => [.. lanes.Where(lane => lane.Id != command.Lane)]);
    }

    private static CommandResult MoveLane(ProjectDocument document, MoveBeatLane command, IIdService ids)
    {
        return Edit(document, command.Lane, lanes => BeatRules.Move(lanes, command.Lane, command.Direction));
    }

    private static CommandResult RenameLane(ProjectDocument document, RenameBeatLane command, IIdService ids)
    {
        return Find(document, command.Lane)
            ?? CommandChecks.Named(command.Name)
            ?? Change(document, command.Lane, lane => lane with { Name = command.Name });
    }

    private static CommandResult SetGain(ProjectDocument document, SetBeatLaneGain command, IIdService ids)
    {
        return Find(document, command.Lane)
            ?? CommandChecks.Range(nameof(command.Gain), command.Gain, 0, ProjectLimits.MaxGain)
            ?? Change(document, command.Lane, lane => lane with { Gain = command.Gain });
    }

    private static CommandResult SetMedia(ProjectDocument document, SetBeatLaneMedia command, IIdService ids)
    {
        return Find(document, command.Lane) ?? Change(document, command.Lane, lane => lane with { Media = command.Media });
    }

    private static CommandResult SetLaneSteps(ProjectDocument document, SetBeatLaneSteps command, IIdService ids)
    {
        if (Find(document, command.Lane) is { } missing)
        {
            return missing;
        }
        int count = document.BeatPattern.Steps;
        if (command.Steps.Count == 0 || command.Steps.Count > ProjectLimits.MaxBeatSteps)
        {
            return CommandChecks.OutOfRange($"Name from 1 to {ProjectLimits.MaxBeatSteps} steps.");
        }
        foreach (int step in command.Steps)
        {
            if (CommandChecks.Range("step", step, 0, count - 1) is { } invalid)
            {
                return invalid;
            }
        }
        HashSet<int> chosen = [.. command.Steps];
        return Change(
            document,
            command.Lane,
            lane => lane with { Steps = [.. lane.Steps.Select((on, step) => chosen.Contains(step) ? command.On : on)] });
    }

    private static CommandResult SetSteps(ProjectDocument document, SetBeatSteps command, IIdService ids)
    {
        if (CommandChecks.Range(nameof(command.Steps), command.Steps, 1, ProjectLimits.MaxBeatSteps) is { } invalid)
        {
            return invalid;
        }
        BeatPattern pattern = document.BeatPattern;
        ValueList<BeatLane> lanes = [.. pattern.Lanes.Select(lane => lane with { Steps = BeatRules.Resize(lane.Steps, command.Steps) })];
        return CommandResult.Ok(document with { BeatPattern = pattern with { Steps = command.Steps, Lanes = lanes } });
    }

    private static CommandResult SetSwing(ProjectDocument document, SetBeatSwing command, IIdService ids)
    {
        return CommandChecks.Range(nameof(command.Swing), command.Swing, 0, 1)
            ?? CommandResult.Ok(document with { BeatPattern = document.BeatPattern with { Swing = command.Swing } });
    }

    // A refusal when no lane has the id; null when there is one.
    private static CommandResult? Find(ProjectDocument document, BeatLaneId id)
    {
        return document.BeatPattern.Lanes.Any(lane => lane.Id == id) ? null : CommandChecks.NotFound("drum lane", id.Value);
    }

    private static CommandResult Change(ProjectDocument document, BeatLaneId id, Func<BeatLane, BeatLane> edit)
    {
        return Edit(document, id, lanes => [.. lanes.Select(lane => lane.Id == id ? edit(lane) : lane)]);
    }

    private static CommandResult Edit(ProjectDocument document, BeatLaneId id, Func<ValueList<BeatLane>, ValueList<BeatLane>> edit)
    {
        BeatPattern pattern = document.BeatPattern;
        return Find(document, id) ?? CommandResult.Ok(document with { BeatPattern = pattern with { Lanes = edit(pattern.Lanes) } });
    }
}
