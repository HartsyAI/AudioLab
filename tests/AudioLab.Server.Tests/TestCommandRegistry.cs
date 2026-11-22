using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Server.Tests;

/// <summary>A small registry of commands for the hub and session tests; the server has no domain commands yet.</summary>
internal static class TestCommandRegistry
{
    public static CommandRegistry Create()
    {
        TestCommandJson json = TestCommandJson.Default;
        return new CommandRegistryBuilder()
            .Add("rename_project", "Renames the project.", json.RenameCommand, (document, command, ids) => CommandResult.Ok(document with { Name = command.Name }))
            .Add("set_bpm", "Sets the tempo.", json.SetBpmCommand, (document, command, ids) => CommandResult.Ok(document with { Bpm = command.Bpm }))
            .Add("add_marker", "Adds a marker and allocates its id.", json.AddMarkerCommand, AddMarker)
            .Add("refuse", "Is always refused.", json.RefuseCommand, (document, command, ids) => CommandResult.Refuse(CommandErrorCode.Rejected, "Not today."))
            .Add("bulk", "Carries text and changes nothing.", json.BulkCommand, (document, command, ids) => CommandResult.Ok(document))
            .Add("no_change", "Changes nothing.", json.NoChangeCommand, (document, command, ids) => CommandResult.Ok(document))
            .Build();
    }

    private static CommandResult AddMarker(ProjectDocument document, AddMarkerCommand command, IIdService ids)
    {
        MarkerId id = ids.Next<MarkerId>();
        Marker marker = new() { Id = id, Time = command.Time, Name = command.Name };
        return CommandResult.Ok(document with { Markers = [.. document.Markers.Append(marker).OrderBy(m => m.Time)] }, id.Value);
    }
}
