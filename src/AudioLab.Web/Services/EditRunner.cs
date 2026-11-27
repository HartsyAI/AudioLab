using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Services;

/// <summary>Runs one edit against the open project and turns its reply, or a lost connection, into an <see cref="EditResult"/>.</summary>
public static class EditRunner
{
    private const string Lost = "The connection to the server was lost, so the edit was not sent.";

    /// <summary>Sends through <paramref name="send"/> with the id of the open project; a refusal or a lost connection is a result.</summary>
    public static async Task<EditResult> RunAsync(LiveProject live, Func<ProjectId, Task<CommandReply>> send)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(send);
        if (live.Document is not { } document)
        {
            return EditResult.Refused("The project is not open yet.");
        }
        try
        {
            CommandReply reply = await send(document.Id);
            return reply.Succeeded ? EditResult.Applied(reply.Changed, reply.Created) : EditResult.Refused(CommandRefusal.Describe(reply));
        }
        catch (Exception exception) when (exception is InvalidOperationException or HubException or OperationCanceledException)
        {
            return EditResult.Refused(exception is HubException hub && !string.IsNullOrWhiteSpace(hub.Message) ? hub.Message : Lost);
        }
    }

    /// <summary>Runs one command as one gesture, which closes when the command has been sent: a drag is one undo step.</summary>
    public static Task<EditResult> RunInGestureAsync(LiveProject live, Func<ProjectId, string, Task<CommandReply>> send)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(send);
        return RunAsync(live, async project =>
        {
            string gesture = live.NewGesture();
            try
            {
                return await send(project, gesture);
            }
            finally
            {
                await live.EndGestureAsync(gesture);
            }
        });
    }
}
