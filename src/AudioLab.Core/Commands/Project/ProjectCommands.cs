using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The project settings commands: the name, the metronome and how the arrangement opens.</summary>
internal static class ProjectCommands
{
    public static void Register(CommandRegistryBuilder builder, ProjectCommandJsonContext json)
    {
        builder
            .Add("project_rename", "Renames the project.", json.RenameProject, Rename)
            .Add("project_set_metronome", "Switches the metronome on or off.", json.SetMetronome, SetMetronome)
            .Add("project_set_view", "Sets the ruler, the edit mode and the grid; at least one is required.", json.SetProjectView, SetView);
    }

    private static CommandResult Rename(ProjectDocument document, RenameProject command, IIdService ids)
    {
        return CommandChecks.Named(command.Name) ?? CommandResult.Ok(document with { Name = command.Name.Trim() });
    }

    private static CommandResult SetMetronome(ProjectDocument document, SetMetronome command, IIdService ids)
    {
        return CommandResult.Ok(document with { Metronome = command.Enabled });
    }

    private static CommandResult SetView(ProjectDocument document, SetProjectView command, IIdService ids)
    {
        if (command.Ruler is null && command.EditMode is null && command.Grid is null)
        {
            return CommandChecks.OutOfRange("Give the ruler, the edit mode, the grid, or any of them.");
        }
        if (command.Grid is not null && !GridRules.Values.Contains(command.Grid))
        {
            return CommandChecks.OutOfRange($"The grid must be one of: {string.Join(", ", GridRules.Values)}.");
        }
        ProjectView view = document.View with
        {
            Ruler = command.Ruler ?? document.View.Ruler,
            EditMode = command.EditMode ?? document.View.EditMode,
            Grid = command.Grid ?? document.View.Grid,
        };
        return CommandResult.Ok(document with { View = view });
    }
}
