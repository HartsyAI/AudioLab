using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Configuration;
using AudioLab.Server.Media;
using AudioLab.Server.Store;

namespace AudioLab.Server.Services;

/// <summary>Edits open projects with commands for the hub and for MCP: parse, apply under the project's order, announce.</summary>
/// <remarks>
/// Every method takes the user from the door and opens the project as that user, so a project of another user is
/// never found. A change and its announcement happen under the project's publish order, so clients hear revisions in order.
/// </remarks>
public sealed class ProjectEditService(OpenProjects open, CommandRegistry registry, IProjectEvents events, AudioLabOptions options, MediaStore media)
{
    /// <summary>Opens the project and, before any later change can be announced, calls <paramref name="subscribe"/>; returns the state to start from.</summary>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="project">The project to join.</param>
    /// <param name="subscribe">Adds the client to the project's members.</param>
    /// <param name="unsubscribe">Takes the client out again, if the project closed while it was being added.</param>
    /// <param name="cancellationToken">Cancels the join.</param>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    public async Task<ProjectSnapshot> JoinAsync(
        UserId user,
        ProjectId project,
        Func<CancellationToken, Task> subscribe,
        Func<CancellationToken, Task> unsubscribe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(unsubscribe);

        // Under the order, a deletion has either finished, so the project is not found, or has not begun, so this is announced to.
        using IDisposable order = await open.EnterOrderAsync(user, project, cancellationToken);
        ProjectSession session = await open.OpenAsync(user, project, cancellationToken);
        RequireNotClosing(session);
        await subscribe(cancellationToken);
        if (session.IsClosed)
        {
            // Closed while the client was being added, which the order does not cover; the client must not be left holding it.
            await unsubscribe(CancellationToken.None);
            throw Closing();
        }
        if (cancellationToken.IsCancellationRequested)
        {
            // The connection ended while it was being added; a dead connection must not stay a member.
            await unsubscribe(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return session.Snapshot;
    }

    /// <summary>Reads a command by its wire name and applies it for a client; a refusal is a reply, not an exception.</summary>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="project">The project the client joined.</param>
    /// <param name="client">The id the server issued to the connection.</param>
    /// <param name="name">The command's wire name.</param>
    /// <param name="json">The command as JSON.</param>
    /// <param name="gesture">The client's gesture, or null for a single edit.</param>
    /// <param name="cancellationToken">Cancels the call before the change is made.</param>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    /// <exception cref="SessionClosedException">The project was closed while the call was made.</exception>
    public async Task<CommandReply> ApplyAsync(
        UserId user,
        ProjectId project,
        ClientId client,
        string? name,
        string? json,
        GestureId? gesture,
        CancellationToken cancellationToken)
    {
        // The media index is read here, where waiting is cheap, so the check inside the session's lock never touches the disk.
        await media.PrepareAsync(user, cancellationToken);
        media.RequireReadable(user);
        using IDisposable order = await open.EnterOrderAsync(user, project, cancellationToken);
        ProjectSession session = await open.OpenAsync(user, project, cancellationToken);
        if (name is null || json is null)
        {
            return Refused([new CommandError(CommandErrorCode.Malformed, "A command needs its name and its JSON text.")], session.Snapshot.Revision);
        }
        if (json.Length > options.HubMaxCommandChars)
        {
            return Refused([new CommandError(CommandErrorCode.Limit, $"The command is longer than the {options.HubMaxCommandChars} characters the server accepts.")], session.Snapshot.Revision);
        }
        CommandParseResult parsed = registry.Parse(name, json);
        if (parsed.Command is not { } command)
        {
            return Refused(parsed.Errors, session.Snapshot.Revision);
        }
        EditOutcome outcome = session.Apply(command, client, gesture);
        return await FinishAsync(user, outcome, name, outcome.Result?.Created.ToArray() ?? []);
    }

    /// <summary>Goes back one undo step for everyone in the project; the reply says whether there was one.</summary>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="project">The project to step back in.</param>
    /// <param name="cancellationToken">Cancels the call before the change is made.</param>
    /// <param name="expectedRevision">When given, the step is taken only if the project is at this revision, else refused as <c>rejected</c>.</param>
    public Task<CommandReply> UndoAsync(UserId user, ProjectId project, CancellationToken cancellationToken, long? expectedRevision = null)
    {
        return StepAsync(user, project, "undo", session => session.Undo(), expectedRevision, cancellationToken);
    }

    /// <summary>Goes forward one redo step for everyone in the project; the reply says whether there was one.</summary>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="project">The project to step forward in.</param>
    /// <param name="cancellationToken">Cancels the call before the change is made.</param>
    /// <param name="expectedRevision">When given, the step is taken only if the project is at this revision, else refused as <c>rejected</c>.</param>
    public Task<CommandReply> RedoAsync(UserId user, ProjectId project, CancellationToken cancellationToken, long? expectedRevision = null)
    {
        return StepAsync(user, project, "redo", session => session.Redo(), expectedRevision, cancellationToken);
    }

    /// <summary>What undo and redo hold in a project of the user's, with the revision it is at.</summary>
    /// <exception cref="ProjectNotFoundException">The user has no such project.</exception>
    public async Task<HistoryState> HistoryAsync(UserId user, ProjectId project, CancellationToken cancellationToken)
    {
        ProjectSession session = await open.OpenAsync(user, project, cancellationToken);
        return session.HistoryState();
    }

    /// <summary>Closes the client's gesture, as the release of a drag does. A project that is not open has nothing to close.</summary>
    public void EndGesture(UserId user, ProjectId project, ClientId client, GestureId gesture)
    {
        open.SessionIfOpen(user, project)?.EndGesture(client, gesture);
    }

    /// <summary>Closes whatever gesture the client has open in the project, as when its connection ends.</summary>
    public void ReleaseClient(UserId user, ProjectId project, ClientId client)
    {
        open.SessionIfOpen(user, project)?.ReleaseClient(client);
    }

    private async Task<CommandReply> StepAsync(
        UserId user,
        ProjectId project,
        string reason,
        Func<ProjectSession, EditOutcome?> step,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        using IDisposable order = await open.EnterOrderAsync(user, project, cancellationToken);
        ProjectSession session = await open.OpenAsync(user, project, cancellationToken);
        if (expectedRevision is { } expected && session.Snapshot.Revision != expected)
        {
            string changed = $"The project is at revision {session.Snapshot.Revision}, not {expected}: it changed since you read it, so nothing was changed; read the history again.";
            return new CommandReply(false, false, session.Snapshot.Revision, [new HubCommandError(CommandErrorCode.Rejected, changed)], []);
        }
        EditOutcome? outcome = step(session);
        if (outcome is null)
        {
            return new CommandReply(false, false, session.Snapshot.Revision, [new HubCommandError(CommandErrorCode.Rejected, $"There is nothing to {reason}.")], []);
        }
        return await FinishAsync(user, outcome, reason, []);
    }

    // The announcement is not tied to the caller's connection: if the sender goes away, the others still hear the change.
    private async Task<CommandReply> FinishAsync(UserId user, EditOutcome outcome, string reason, string[] created)
    {
        if (outcome.Result is { Succeeded: false } refused)
        {
            return Refused(refused.Errors, outcome.Snapshot.Revision);
        }
        if (outcome.Changed)
        {
            await events.ChangedAsync(user, outcome.Snapshot, outcome.Json!, reason, CancellationToken.None);
        }
        return new CommandReply(true, outcome.Changed, outcome.Snapshot.Revision, [], created);
    }

    private static void RequireNotClosing(ProjectSession session)
    {
        if (session.IsClosed)
        {
            throw Closing();
        }
    }

    private static ProjectNotFoundException Closing() => new("The project is closing; join it again.");

    private static CommandReply Refused(IEnumerable<CommandError> errors, long revision)
    {
        return new CommandReply(false, false, revision, [.. errors.Select(error => new HubCommandError(error.Code, error.Message))], []);
    }
}
