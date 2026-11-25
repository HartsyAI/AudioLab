using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;

namespace AudioLab.Web.Services;

/// <summary>The edits of a track group, each one command sent by wire name; the server decides and its reasons come back as words.</summary>
public sealed class GroupEditor(LiveProject live)
{
    /// <summary>Folds a group to one row in the mixer, or shows its tracks.</summary>
    public Task<EditResult> SetCollapsedAsync(GroupId group, bool collapsed)
    {
        return RunAsync(project => live.SendAsync(new SetGroupCollapsed { Project = project, Group = group, Collapsed = collapsed }));
    }

    /// <summary>Sets the gain multiplier of a group, as one gesture: a drag is one undo step.</summary>
    public Task<EditResult> SetVolumeAsync(GroupId group, double volume)
    {
        return EditRunner.RunInGestureAsync(
            live,
            (project, gesture) => live.SendAsync(new SetGroupVolume { Project = project, Group = group, Volume = volume }, gesture));
    }

    /// <summary>Mutes or unmutes every member of a group.</summary>
    public Task<EditResult> SetMutedAsync(GroupId group, bool muted)
    {
        return RunAsync(project => live.SendAsync(new SetGroupMuted { Project = project, Group = group, Muted = muted }));
    }

    /// <summary>Solos or unsolos every member of a group.</summary>
    public Task<EditResult> SetSoloedAsync(GroupId group, bool soloed)
    {
        return RunAsync(project => live.SendAsync(new SetGroupSoloed { Project = project, Group = group, Soloed = soloed }));
    }

    /// <summary>Chooses whether the clips of a group draw in its colour.</summary>
    public Task<EditResult> SetTintAsync(GroupId group, bool tint)
    {
        return RunAsync(project => live.SendAsync(new SetGroupTint { Project = project, Group = group, Tint = tint }));
    }

    /// <summary>Recolours a group.</summary>
    public Task<EditResult> SetColorAsync(GroupId group, string color)
    {
        return RunAsync(project => live.SendAsync(new SetGroupColor { Project = project, Group = group, Color = color }));
    }

    private Task<EditResult> RunAsync(Func<ProjectId, Task<CommandReply>> send) => EditRunner.RunAsync(live, send);
}
