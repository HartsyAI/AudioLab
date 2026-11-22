using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

/// <summary>A registry of the test commands, and a valid project for them to edit.</summary>
internal static class TestRegistry
{
    public static CommandRegistry Create()
    {
        TestCommandJsonContext json = TestCommandJsonContext.Default;
        return new CommandRegistryBuilder()
            .Add("rename_project", "Renames the project.", json.RenameProjectCommand, Rename)
            .Add("set_bpm", "Sets the project tempo.", json.SetBpmCommand, SetBpm)
            .Add("add_marker", "Adds a marker and allocates its id.", json.AddMarkerCommand, AddMarker)
            .Add("nothing", "Changes nothing.", json.NothingCommand, (document, _, _) => CommandResult.Ok(document))
            .Add("refused", "Is always refused.", json.RefusedCommand, Refused)
            .Add("hijack", "Tries to give the project to another user.", json.HijackCommand, Hijack)
            .Add("break", "Adds markers with no name.", json.BreakCommand, Break)
            .Add("shape", "Carries every kind of member.", json.ShapeCommand, (document, _, _) => CommandResult.Ok(document))
            .Build();
    }

    public static ProjectDocument Project() => SampleProject.Create(UserId.Local);

    // Starts above the sample project's own ids so a minted id never collides with one in the document.
    public static SequentialIdService Ids() => new(1000);

    private static CommandResult Rename(ProjectDocument document, RenameProjectCommand command, IIdService ids)
    {
        return CommandResult.Ok(document with { Name = command.Name });
    }

    private static CommandResult SetBpm(ProjectDocument document, SetBpmCommand command, IIdService ids)
    {
        return CommandResult.Ok(document with { Bpm = command.Bpm });
    }

    private static CommandResult AddMarker(ProjectDocument document, AddMarkerCommand command, IIdService ids)
    {
        MarkerId id = ids.Next<MarkerId>();
        Marker marker = new() { Id = id, Time = command.Time, Name = command.Name };
        ProjectDocument edited = document with { Markers = [.. document.Markers, marker] };
        return CommandResult.Ok(edited, id.Value);
    }

    private static CommandResult Refused(ProjectDocument document, RefusedCommand command, IIdService ids)
    {
        return CommandResult.Refuse(CommandErrorCode.NotFound, "There is nothing to refuse.");
    }

    private static CommandResult Hijack(ProjectDocument document, HijackCommand command, IIdService ids)
    {
        return CommandResult.Ok(document with { Owner = command.NewOwner });
    }

    private static CommandResult Break(ProjectDocument document, BreakCommand command, IIdService ids)
    {
        List<Marker> markers = [.. document.Markers];
        for (int i = 0; i < command.Count; i++)
        {
            markers.Add(new Marker { Id = ids.Next<MarkerId>(), Time = 1000 + i, Name = string.Empty });
        }
        return CommandResult.Ok(document with { Markers = new ValueList<Marker>(markers) });
    }
}
