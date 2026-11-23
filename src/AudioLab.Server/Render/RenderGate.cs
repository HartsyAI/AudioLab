using AudioLab.Core.Model;

namespace AudioLab.Server.Render;

/// <summary>Limits how many exports run at once, for each user and for the server, and never makes a request wait.</summary>
public sealed class RenderGate(RenderOptions options)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<UserId, int> _perUser = [];
    private int _total;

    /// <summary>How many exports are running.</summary>
    public int Running
    {
        get
        {
            lock (_lock)
            {
                return _total;
            }
        }
    }

    /// <summary>Takes a place for an export of <paramref name="user"/>, or says why there is none.</summary>
    /// <exception cref="ExportRefusedException">The user or the server is already running as many exports as are allowed.</exception>
    public IDisposable Enter(UserId user)
    {
        lock (_lock)
        {
            _perUser.TryGetValue(user, out int mine);
            if (mine >= options.MaxConcurrentExportsPerUser)
            {
                throw new ExportRefusedException(
                    ExportRefusal.Busy,
                    $"You already have {mine} exports running; the most is {options.MaxConcurrentExportsPerUser}. Try again when one has finished.");
            }
            if (_total >= options.MaxConcurrentExports)
            {
                throw new ExportRefusedException(ExportRefusal.Busy, "The server is running as many exports as it can; try again shortly.");
            }
            _perUser[user] = mine + 1;
            _total++;
        }
        return new Lease(this, user);
    }

    private void Leave(UserId user)
    {
        lock (_lock)
        {
            _total--;
            if (--_perUser[user] == 0)
            {
                _perUser.Remove(user);
            }
        }
    }

    private sealed class Lease(RenderGate gate, UserId user) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Leave(user);
            }
        }
    }
}
