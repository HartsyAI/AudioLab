using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Server.Realtime;

/// <summary>What the hub knows of one connection: its user, its client id, the projects it joined and its outbox.</summary>
public sealed class ConnectionState
{
    private readonly object _sync = new();
    private readonly HashSet<ProjectId> _joined = [];

    /// <summary>Creates the state of a connection; the client id is the connection id the server issued.</summary>
    public ConnectionState(UserId user, string connectionId, ConnectionOutbox outbox)
    {
        User = user;
        Client = ClientId.From(connectionId);
        Outbox = outbox;
    }

    /// <summary>The user the door resolved when the connection was made.</summary>
    public UserId User { get; }

    /// <summary>The id <see cref="CommandHistory"/> knows this client by.</summary>
    public ClientId Client { get; }

    /// <summary>What is yet to be sent to the connection.</summary>
    public ConnectionOutbox Outbox { get; }

    /// <summary>How many projects the connection has joined.</summary>
    public int JoinedCount
    {
        get
        {
            lock (_sync)
            {
                return _joined.Count;
            }
        }
    }

    /// <summary>Whether the connection has joined the project.</summary>
    public bool IsJoined(ProjectId project)
    {
        lock (_sync)
        {
            return _joined.Contains(project);
        }
    }

    /// <summary>Records a join; false when it was already joined.</summary>
    public bool Add(ProjectId project)
    {
        lock (_sync)
        {
            return _joined.Add(project);
        }
    }

    /// <summary>Records a leave, or the end of a deleted project; false when it was not joined.</summary>
    public bool Remove(ProjectId project)
    {
        lock (_sync)
        {
            return _joined.Remove(project);
        }
    }

    /// <summary>The projects the connection has joined.</summary>
    public IReadOnlyList<ProjectId> Joined()
    {
        lock (_sync)
        {
            return [.. _joined];
        }
    }
}
