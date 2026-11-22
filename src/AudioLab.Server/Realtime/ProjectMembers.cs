using AudioLab.Core.Model;

namespace AudioLab.Server.Realtime;

/// <summary>Which connections have joined which project. The owner is part of the key, so one user's connections are never in another user's project.</summary>
public sealed class ProjectMembers
{
    private readonly object _sync = new();
    private readonly Dictionary<(UserId User, ProjectId Project), HashSet<ConnectionState>> _members = [];

    /// <summary>Adds a connection to a project.</summary>
    public void Add(UserId user, ProjectId project, ConnectionState connection)
    {
        lock (_sync)
        {
            if (!_members.TryGetValue((user, project), out HashSet<ConnectionState>? set))
            {
                set = [];
                _members[(user, project)] = set;
            }
            set.Add(connection);
        }
    }

    /// <summary>Removes a connection from a project.</summary>
    public void Remove(UserId user, ProjectId project, ConnectionState connection)
    {
        lock (_sync)
        {
            if (_members.TryGetValue((user, project), out HashSet<ConnectionState>? set) && set.Remove(connection) && set.Count == 0)
            {
                _members.Remove((user, project));
            }
        }
    }

    /// <summary>Removes a connection from every project it joined.</summary>
    public void RemoveAll(ConnectionState connection)
    {
        foreach (ProjectId project in connection.Joined())
        {
            Remove(connection.User, project, connection);
        }
    }

    /// <summary>The connections that have joined the project.</summary>
    public IReadOnlyList<ConnectionState> Of(UserId user, ProjectId project)
    {
        lock (_sync)
        {
            return _members.TryGetValue((user, project), out HashSet<ConnectionState>? set) ? [.. set] : [];
        }
    }

    /// <summary>Ends a project's membership and returns the connections that had joined it.</summary>
    public IReadOnlyList<ConnectionState> Close(UserId user, ProjectId project)
    {
        lock (_sync)
        {
            if (!_members.Remove((user, project), out HashSet<ConnectionState>? set))
            {
                return [];
            }
            return [.. set];
        }
    }
}
