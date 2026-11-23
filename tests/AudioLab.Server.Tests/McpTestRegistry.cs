using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Server.Tests;

/// <summary>Small registries for the MCP tests, each built from the same pieces so one can be the other plus a command.</summary>
internal static class McpTestRegistry
{
    public static CommandRegistry Create(bool withMarker = true, bool withFailures = false, string? extraName = null)
    {
        TestCommandJson json = TestCommandJson.Default;
        CommandRegistryBuilder builder = new CommandRegistryBuilder()
            .Add("rename_project", "Renames the project.", json.RenameCommand, (document, command, ids) => Ok(document with { Name = command.Name }))
            .Add("set_bpm", "Sets the tempo.", json.SetBpmCommand, (document, command, ids) => Ok(document with { Bpm = command.Bpm }));
        if (withMarker)
        {
            builder = builder.Add("add_marker", "Adds a marker and allocates its id.", json.AddMarkerCommand, AddMarker);
        }
        if (withFailures)
        {
            builder = builder
                .Add("fail_with", "Is refused with the class it is told.", json.FailWithCommand, (document, command, ids) => Fail(command))
                .Add("bad_bpm", "Leaves a tempo the validator refuses.", json.BadBpmCommand, (d, c, ids) => Ok(d with { Bpm = 0 }))
                .Add("no_change", "Changes nothing.", json.NoChangeCommand, (document, command, ids) => Ok(document))
                .Add("add_many", "Adds markers and allocates an id for each.", json.AddManyCommand, AddMany);
        }
        if (extraName is not null)
        {
            builder = builder.Add(extraName, "Added to the registry and nowhere else.", json.NoChangeCommand, (d, c, ids) => Ok(d));
        }
        return builder.Build();
    }

    private static CommandResult Ok(ProjectDocument document) => CommandResult.Ok(document);

    private static CommandResult Fail(FailWithCommand command)
    {
        CommandErrorCode code = Enum.GetValues<CommandErrorCode>().Single(candidate => Wire(candidate) == command.Code);
        return CommandResult.Refuse(code, $"Refused as {command.Code}.");
    }

    public static string Wire(CommandErrorCode code)
    {
        return System.Text.Json.JsonSerializer.Serialize(code).Trim('"');
    }

    private static CommandResult AddMany(ProjectDocument document, AddManyCommand command, IIdService ids)
    {
        Marker[] added = [.. Enumerable.Range(0, command.Count).Select(index => new Marker { Id = ids.Next<MarkerId>(), Time = index, Name = "M" })];
        ProjectDocument edited = document with { Markers = [.. document.Markers.Concat(added).OrderBy(marker => marker.Time)] };
        return CommandResult.Ok(edited, [.. added.Select(marker => marker.Id.Value)]);
    }

    private static CommandResult AddMarker(ProjectDocument document, AddMarkerCommand command, IIdService ids)
    {
        MarkerId id = ids.Next<MarkerId>();
        Marker marker = new() { Id = id, Time = command.Time, Name = command.Name };
        ProjectDocument edited = document with { Markers = [.. document.Markers.Append(marker).OrderBy(m => m.Time)] };
        return CommandResult.Ok(edited, id.Value);
    }
}
