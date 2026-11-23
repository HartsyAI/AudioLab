using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Services;

/// <summary>Renames a project not open on screen; a project changes only by command (D2): join, send <c>project_rename</c>, leave.</summary>
public sealed class ProjectRenamer(IProjectHubFactory hubs, CommandSender sender)
{
    /// <summary>Renames the project; a refusal, such as a blank name, is in the reply.</summary>
    public async Task<CommandReply> RenameAsync(string project, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentNullException.ThrowIfNull(name);
        await using IProjectHub hub = hubs.Create(reconnects: false);
        await hub.ConnectAsync(cancellationToken);
        await hub.JoinAsync(project, cancellationToken);
        RenameProject command = new() { Project = ProjectId.From(project), Name = name };
        CommandReply reply = await sender.SendAsync(hub, command, null, cancellationToken);
        try
        {
            await hub.LeaveAsync(project, CancellationToken.None);
        }
        catch (Exception exception) when (exception is HubException or InvalidOperationException)
        {
            // The rename is made; a connection that is about to be closed anyway need not say so.
        }
        return reply;
    }
}
