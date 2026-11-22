using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The group commands; a group's members stay together in the track list and an empty group does not exist.</summary>
internal static class GroupCommands
{
    public static void Register(CommandRegistryBuilder builder, TrackCommandJsonContext json)
    {
        builder
            .Add("group_create", "Puts tracks in a new group and allocates the group id.", json.CreateGroup, Create)
            .Add("group_assign", "Moves tracks into a group, or out of their group.", json.AssignToGroup, Assign)
            .Add("group_ungroup", "Takes every track out of a group and removes it.", json.Ungroup, Ungroup)
            .Add("group_rename", "Renames a group.", json.RenameGroup, Rename)
            .Add("group_set_color", "Recolours a group.", json.SetGroupColor, SetColor)
            .Add("group_set_collapsed", "Folds a group to one row or shows its tracks.", json.SetGroupCollapsed, SetCollapsed)
            .Add("group_set_tint", "Chooses whether the clips of a group draw in its colour.", json.SetGroupTint, SetTint)
            .Add("group_set_muted", "Mutes or unmutes every member of a group.", json.SetGroupMuted, SetMuted)
            .Add("group_set_soloed", "Solos or unsolos every member of a group.", json.SetGroupSoloed, SetSoloed)
            .Add("group_set_volume", "Sets the gain multiplier of a group.", json.SetGroupVolume, SetVolume)
            .Add("group_move", "Moves a whole group one step in the track list.", json.MoveGroup, Move);
    }

    private static CommandResult Create(ProjectDocument document, CreateGroup command, IIdService ids)
    {
        if (CommandChecks.TooLong(command.Name) is { } tooLong)
        {
            return tooLong;
        }
        if (TrackEdits.CheckTracks(document, command.Tracks) is { } refusal)
        {
            return refusal;
        }
        TrackGroup group = new()
        {
            Id = ids.Next<GroupId>(),
            Name = string.IsNullOrWhiteSpace(command.Name) ? GroupRules.UniqueName(document.Groups) : command.Name.Trim(),
            Color = command.Color ?? ProjectDefaults.TrackColor(document.Groups.Count),
            Collapsed = false,
            Tint = true,
            Muted = false,
            Soloed = false,
            Volume = 1,
        };
        GroupedTracks grouped = GroupRules.Assign(document.Tracks, [.. document.Groups, group], [.. command.Tracks], group.Id);
        return CommandResult.Ok(Apply(document, grouped), group.Id.Value);
    }

    private static CommandResult Assign(ProjectDocument document, AssignToGroup command, IIdService ids)
    {
        if (TrackEdits.CheckTracks(document, command.Tracks) is { } refusal)
        {
            return refusal;
        }
        if (command.Group is { } group && !document.Groups.Any(candidate => candidate.Id == group))
        {
            return CommandChecks.NotFound("group", group.Value);
        }
        GroupedTracks grouped = GroupRules.Assign(document.Tracks, document.Groups, [.. command.Tracks], command.Group);
        return CommandResult.Ok(Apply(document, grouped));
    }

    private static CommandResult Ungroup(ProjectDocument document, Ungroup command, IIdService ids)
    {
        if (!document.Groups.Any(group => group.Id == command.Group))
        {
            return CommandChecks.NotFound("group", command.Group.Value);
        }
        IReadOnlyCollection<TrackId> members = [.. GroupRules.MembersOf(command.Group, document.Tracks).Select(track => track.Id)];
        return CommandResult.Ok(Apply(document, GroupRules.Assign(document.Tracks, document.Groups, members, null)));
    }

    private static CommandResult Rename(ProjectDocument document, RenameGroup command, IIdService ids)
    {
        return CommandChecks.Named(command.Name) ?? Replace(document, command.Group, group => group with { Name = command.Name.Trim() });
    }

    private static CommandResult SetColor(ProjectDocument document, SetGroupColor command, IIdService ids)
    {
        return Replace(document, command.Group, group => group with { Color = command.Color });
    }

    private static CommandResult SetCollapsed(ProjectDocument document, SetGroupCollapsed command, IIdService ids)
    {
        return Replace(document, command.Group, group => group with { Collapsed = command.Collapsed });
    }

    private static CommandResult SetTint(ProjectDocument document, SetGroupTint command, IIdService ids)
    {
        return Replace(document, command.Group, group => group with { Tint = command.Tint });
    }

    private static CommandResult SetMuted(ProjectDocument document, SetGroupMuted command, IIdService ids)
    {
        return Replace(document, command.Group, group => group with { Muted = command.Muted });
    }

    private static CommandResult SetSoloed(ProjectDocument document, SetGroupSoloed command, IIdService ids)
    {
        return Replace(document, command.Group, group => group with { Soloed = command.Soloed });
    }

    private static CommandResult SetVolume(ProjectDocument document, SetGroupVolume command, IIdService ids)
    {
        return CommandChecks.Finite((nameof(command.Volume), command.Volume))
            ?? Replace(document, command.Group, group => group with { Volume = command.Volume });
    }

    private static CommandResult Move(ProjectDocument document, MoveGroup command, IIdService ids)
    {
        if (!document.Groups.Any(group => group.Id == command.Group))
        {
            return CommandChecks.NotFound("group", command.Group.Value);
        }
        IReadOnlyList<Track>? moved = GroupRules.MoveGroup(document.Tracks, command.Group, command.Direction);
        return CommandResult.Ok(moved is null ? document : document with { Tracks = [.. moved] });
    }

    private static ProjectDocument Apply(ProjectDocument document, GroupedTracks grouped)
    {
        return document with { Tracks = [.. grouped.Tracks], Groups = [.. grouped.Groups] };
    }

    private static CommandResult Replace(ProjectDocument document, GroupId id, Func<TrackGroup, TrackGroup> edit)
    {
        if (!document.Groups.Any(group => group.Id == id))
        {
            return CommandChecks.NotFound("group", id.Value);
        }
        ValueList<TrackGroup> groups = [.. document.Groups.Select(group => group.Id == id ? edit(group) : group)];
        return CommandResult.Ok(document with { Groups = groups });
    }
}
