using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The tempo commands: the project tempo, the time signature and the tempo changes after the start.</summary>
internal static class TempoCommands
{
    public static void Register(CommandRegistryBuilder builder, TimelineCommandJsonContext json)
    {
        builder
            .Add("tempo_set_bpm", "Sets the tempo from the start until the first tempo change.", json.SetTempoBpm, SetBpm)
            .Add("time_signature_set", "Sets the time signature of the project.", json.SetTimeSignature, SetSignature)
            .Add("tempo_change_add", "Adds a tempo change and allocates its id.", json.AddTempoChange, Add)
            .Add("tempo_change_set_bpm", "Sets the tempo of one tempo change.", json.SetTempoChangeBpm, SetChangeBpm)
            .Add("tempo_change_move", "Moves a tempo change in time.", json.MoveTempoChange, Move)
            .Add("tempo_change_remove", "Removes a tempo change.", json.RemoveTempoChange, Remove);
    }

    private static CommandResult SetBpm(ProjectDocument document, SetTempoBpm command, IIdService ids)
    {
        return CommandChecks.Finite((nameof(command.Bpm), command.Bpm)) ?? CommandResult.Ok(document with { Bpm = command.Bpm });
    }

    private static CommandResult SetSignature(ProjectDocument document, SetTimeSignature command, IIdService ids)
    {
        TimeSignature signature = new(command.Numerator, command.Denominator);
        return CommandResult.Ok(document with { TimeSignature = signature });
    }

    private static CommandResult Add(ProjectDocument document, AddTempoChange command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Time), command.Time), (nameof(command.Bpm), command.Bpm)) is { } refusal)
        {
            return refusal;
        }
        if (command.Time < TimelineRules.MinTempoChangeTime)
        {
            return CommandChecks.OutOfRange("A tempo change starts at 0.01 seconds or later; set the project tempo for the start.");
        }
        if (document.TempoMap.Any(change => Math.Abs(change.Time - command.Time) < TimelineRules.SameSpot))
        {
            return CommandChecks.Rejected("There is already a tempo change here.");
        }
        TempoChange added = new(ids.Next<TempoChangeId>(), command.Time, command.Bpm);
        ValueList<TempoChange> changes = [.. document.TempoMap.Append(added).OrderBy(change => change.Time)];
        return CommandResult.Ok(document with { TempoMap = changes }, added.Id.Value);
    }

    private static CommandResult SetChangeBpm(ProjectDocument document, SetTempoChangeBpm command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Bpm), command.Bpm)) is { } refusal)
        {
            return refusal;
        }
        if (!document.TempoMap.Any(change => change.Id == command.Change))
        {
            return CommandChecks.NotFound("tempo change", command.Change.Value);
        }
        ValueList<TempoChange> changes = [.. document.TempoMap.Select(change => change.Id == command.Change ? change with { Bpm = command.Bpm } : change)];
        return CommandResult.Ok(document with { TempoMap = changes });
    }

    private static CommandResult Move(ProjectDocument document, MoveTempoChange command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Time), command.Time)) is { } refusal)
        {
            return refusal;
        }
        if (!document.TempoMap.Any(change => change.Id == command.Change))
        {
            return CommandChecks.NotFound("tempo change", command.Change.Value);
        }
        double time = Math.Max(TimelineRules.MinTempoChangeTime, command.Time);
        TempoChange current = document.TempoMap.First(change => change.Id == command.Change);
        if (Math.Abs(current.Time - time) < TimelineRules.TempoNoMove)
        {
            return CommandResult.Ok(document);
        }
        if (document.TempoMap.Any(change => change.Id != command.Change && Math.Abs(change.Time - time) < TimelineRules.SameSpot))
        {
            return CommandChecks.Rejected("There is already a tempo change here.");
        }
        ValueList<TempoChange> changes =
            [.. document.TempoMap.Select(change => change.Id == command.Change ? change with { Time = time } : change).OrderBy(change => change.Time)];
        return CommandResult.Ok(document with { TempoMap = changes });
    }

    private static CommandResult Remove(ProjectDocument document, RemoveTempoChange command, IIdService ids)
    {
        if (!document.TempoMap.Any(change => change.Id == command.Change))
        {
            return CommandChecks.NotFound("tempo change", command.Change.Value);
        }
        ValueList<TempoChange> changes = [.. document.TempoMap.Where(change => change.Id != command.Change)];
        return CommandResult.Ok(document with { TempoMap = changes });
    }
}
