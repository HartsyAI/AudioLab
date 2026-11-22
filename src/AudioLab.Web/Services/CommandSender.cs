using AudioLab.Core.Commands;
using AudioLab.Core.Realtime;

namespace AudioLab.Web.Services;

/// <summary>Sends a typed command over a hub by its wire name, written by the registry so the server reads exactly what was built here.</summary>
public sealed class CommandSender(CommandRegistry registry)
{
    /// <summary>Sends <paramref name="command"/>; a refusal is in the reply, never an exception.</summary>
    /// <param name="hub">The connection, which has joined the command's project.</param>
    /// <param name="command">The command; its type must be in the registry.</param>
    /// <param name="gesture">The id of the gesture the command belongs to, or null for a single edit.</param>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <exception cref="InvalidOperationException">The command's type is not registered, which is a bug in the caller.</exception>
    public Task<CommandReply> SendAsync(IProjectHub hub, Command command, string? gesture = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(command);
        if (!registry.TryWrite(command, out string? name, out string? json))
        {
            throw new InvalidOperationException($"{command.GetType().Name} is not a registered command.");
        }
        return hub.ApplyAsync(command.Project.Value, name, json, gesture, cancellationToken);
    }
}
