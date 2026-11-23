using AudioLab.Core.Model;

namespace AudioLab.Server.Mcp;

/// <summary>Limits how many MCP requests one user has in flight at once, as the hub limits the calls of one connection.</summary>
/// <remarks>A request over the limit is not queued: the caller is told to try again, so a stuck client cannot pile up requests.</remarks>
public sealed class McpCallLimiter(int maxPerUser)
{
    private readonly object _lock = new();
    private readonly Dictionary<UserId, int> _active = [];

    /// <summary>Takes one of the user's slots, or returns null when all are in use. Dispose the result to give the slot back.</summary>
    public IDisposable? TryEnter(UserId user)
    {
        lock (_lock)
        {
            int count = _active.GetValueOrDefault(user);
            if (count >= maxPerUser)
            {
                return null;
            }
            _active[user] = count + 1;
        }
        return new Slot(this, user);
    }

    private void Leave(UserId user)
    {
        lock (_lock)
        {
            int count = _active[user] - 1;
            if (count == 0)
            {
                _active.Remove(user);
            }
            else
            {
                _active[user] = count;
            }
        }
    }

    private sealed class Slot(McpCallLimiter owner, UserId user) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.Leave(user);
            }
        }
    }
}
