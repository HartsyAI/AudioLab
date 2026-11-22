using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The marker commands; markers stay in time order.</summary>
internal static class MarkerCommands
{
    public static void Register(CommandRegistryBuilder builder, TimelineCommandJsonContext json)
    {
        builder
            .Add("marker_add", "Adds a marker and allocates its id.", json.AddMarker, Add)
            .Add("marker_rename", "Renames a marker.", json.RenameMarker, Rename)
            .Add("marker_move", "Moves a marker in time.", json.MoveMarker, Move)
            .Add("marker_set_color", "Sets a marker's colour, or clears it.", json.SetMarkerColor, SetColor)
            .Add("marker_remove", "Removes a marker.", json.RemoveMarker, Remove);
    }

    private static CommandResult Add(ProjectDocument document, AddMarker command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Time), command.Time)) is { } refusal)
        {
            return refusal;
        }
        if (command.Time < 0)
        {
            return CommandChecks.Rejected("A marker cannot be before the start.");
        }
        double time = command.Time;
        if (document.Markers.Any(marker => Math.Abs(marker.Time - time) < TimelineRules.SameSpot))
        {
            return CommandChecks.Rejected("There is already a marker here.");
        }
        string number = (document.Markers.Count + 1).ToString(CultureInfo.InvariantCulture);
        string name = string.IsNullOrWhiteSpace(command.Name) ? $"Marker {number}" : command.Name.Trim();
        Marker added = new() { Id = ids.Next<MarkerId>(), Time = time, Name = name };
        ValueList<Marker> markers = [.. document.Markers.Append(added).OrderBy(marker => marker.Time)];
        return CommandResult.Ok(document with { Markers = markers }, added.Id.Value);
    }

    private static CommandResult Rename(ProjectDocument document, RenameMarker command, IIdService ids)
    {
        if (CommandChecks.Named(command.Name) is { } refusal)
        {
            return refusal;
        }
        return Replace(document, command.Marker, marker => marker with { Name = command.Name.Trim() });
    }

    private static CommandResult Move(ProjectDocument document, MoveMarker command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Time), command.Time)) is { } refusal)
        {
            return refusal;
        }
        double time = Math.Max(0, command.Time);
        Marker? current = document.Markers.FirstOrDefault(marker => marker.Id == command.Marker);
        if (current is null)
        {
            return CommandChecks.NotFound("marker", command.Marker.Value);
        }
        if (Math.Abs(current.Time - time) < TimelineRules.NoMove)
        {
            return CommandResult.Ok(document);
        }
        if (document.Markers.Any(marker => marker.Id != command.Marker && Math.Abs(marker.Time - time) < TimelineRules.SameSpot))
        {
            return CommandChecks.Rejected("There is already a marker here.");
        }
        return Replace(document, command.Marker, marker => marker with { Time = time });
    }

    private static CommandResult SetColor(ProjectDocument document, SetMarkerColor command, IIdService ids)
    {
        return Replace(document, command.Marker, marker => marker with { Color = command.Color });
    }

    private static CommandResult Remove(ProjectDocument document, RemoveMarker command, IIdService ids)
    {
        if (!document.Markers.Any(marker => marker.Id == command.Marker))
        {
            return CommandChecks.NotFound("marker", command.Marker.Value);
        }
        ValueList<Marker> markers = [.. document.Markers.Where(marker => marker.Id != command.Marker)];
        return CommandResult.Ok(document with { Markers = markers });
    }

    private static CommandResult Replace(ProjectDocument document, MarkerId id, Func<Marker, Marker> edit)
    {
        if (!document.Markers.Any(marker => marker.Id == id))
        {
            return CommandChecks.NotFound("marker", id.Value);
        }
        ValueList<Marker> markers = [.. document.Markers.Select(marker => marker.Id == id ? edit(marker) : marker).OrderBy(marker => marker.Time)];
        return CommandResult.Ok(document with { Markers = markers });
    }
}
