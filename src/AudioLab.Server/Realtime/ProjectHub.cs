using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Server.Realtime;

/// <summary>The hub: a client joins a project it owns, sends commands and hears every change to it, whoever made it; the user is the one the door resolved and the client id is the connection id the server issued.</summary>
public sealed class ProjectHub(ProjectEditService edits, ProjectMembers members, ConnectionOutboxFactory outboxes) : Hub<IProjectClient>
{
    /// <summary>The most projects one connection may have joined.</summary>
    public const int MaxProjectsPerConnection = 16;

    private const string StateKey = "audiolab.connection";

    private ConnectionState State => (ConnectionState)Context.Items[StateKey]!;

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        UserId user = Context.GetHttpContext()?.RequireUser() ?? throw new HubException("The connection has no user.");
        Context.Items[StateKey] = new ConnectionState(user, Context.ConnectionId, outboxes.Create(Context));
        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(StateKey, out object? value) && value is ConnectionState state)
        {
            members.RemoveAll(state);
            foreach (ProjectId project in state.Joined())
            {
                edits.ReleaseClient(state.User, project, state.Client);
            }
            state.Outbox.Dispose();
        }
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Joins a project the user owns and returns its state; every later change is sent with a higher revision, and none is missed.</summary>
    /// <remarks>A change may reach the client before this call returns; the client keeps it and applies it if its revision is above the reply's.</remarks>
    [HubMethodName("Join")]
    public async Task<JoinReply> JoinAsync(string? project)
    {
        ProjectId id = Parse(project);
        ConnectionState state = State;
        if (!state.IsJoined(id) && state.JoinedCount >= MaxProjectsPerConnection)
        {
            throw new HubException($"A connection may join {MaxProjectsPerConnection} projects at most.");
        }
        ProjectSnapshot snapshot = await GuardAsync(() => edits.JoinAsync(
            state.User,
            id,
            _ =>
            {
                members.Add(state.User, id, state);
                state.Add(id);
                return Task.CompletedTask;
            },
            Context.ConnectionAborted));
        return new JoinReply(id.Value, snapshot.Revision, ProjectJson.Write(snapshot.Document));
    }

    /// <summary>Leaves a project: no more changes are sent, and an open gesture is closed.</summary>
    [HubMethodName("Leave")]
    public Task LeaveAsync(string? project)
    {
        ProjectId id = Parse(project);
        ConnectionState state = State;
        if (state.Remove(id))
        {
            members.Remove(state.User, id, state);
            edits.ReleaseClient(state.User, id, state.Client);
        }
        return Task.CompletedTask;
    }

    /// <summary>Applies a command to a joined project. A refusal is in the reply; nothing is thrown for it.</summary>
    [HubMethodName("Apply")]
    public Task<CommandReply> ApplyAsync(string? project, string? command, string? json, string? gesture)
    {
        ProjectId id = RequireJoined(project);
        GestureId? gestureId = gesture is null ? null : ParseGesture(gesture);
        return GuardAsync(() => edits.ApplyAsync(State.User, id, State.Client, command, json, gestureId, Context.ConnectionAborted));
    }

    /// <summary>Goes back one undo step in a joined project, for everyone in it.</summary>
    [HubMethodName("Undo")]
    public Task<CommandReply> UndoAsync(string? project)
    {
        ProjectId id = RequireJoined(project);
        return GuardAsync(() => edits.UndoAsync(State.User, id, Context.ConnectionAborted));
    }

    /// <summary>Goes forward one redo step in a joined project, for everyone in it.</summary>
    [HubMethodName("Redo")]
    public Task<CommandReply> RedoAsync(string? project)
    {
        ProjectId id = RequireJoined(project);
        return GuardAsync(() => edits.RedoAsync(State.User, id, Context.ConnectionAborted));
    }

    /// <summary>Closes this client's open gesture, as the release of a drag does.</summary>
    [HubMethodName("EndGesture")]
    public Task EndGestureAsync(string? project, string? gesture)
    {
        ProjectId id = RequireJoined(project);
        edits.EndGesture(State.User, id, State.Client, ParseGesture(gesture));
        return Task.CompletedTask;
    }

    private static ProjectId Parse(string? project)
    {
        return IdRules.IsValid(project) ? ProjectId.From(project!) : throw new HubException("That is not a project id.");
    }

    private static GestureId ParseGesture(string? gesture)
    {
        return IdRules.IsValid(gesture) ? GestureId.From(gesture!) : throw new HubException("That is not a gesture id.");
    }

    // A client may only act on a project it joined, which is also how its changes come back to it.
    private ProjectId RequireJoined(string? project)
    {
        ProjectId id = Parse(project);
        return State.IsJoined(id) ? id : throw new HubException("Join the project first.");
    }

    // Expected failures reach the client as a short message; anything else is the server's bug and says nothing of its own.
    private static async Task<T> GuardAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ProjectNotFoundException)
        {
            throw new HubException("There is no such project.");
        }
        catch (SessionClosedException)
        {
            throw new HubException("The project was closed or deleted; join it again.");
        }
        catch (ProjectFormatException)
        {
            throw new HubException("The project file is not valid.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new HubException("The project store is not available right now; try again.");
        }
    }
}
