using AudioLab.Core.Model;

namespace AudioLab.Server.Services;

/// <summary>Limits how many jobs of one kind run at once, for each user and for the server, and never makes a request wait.</summary>
/// <param name="perUser">The most one user may have running.</param>
/// <param name="total">The most the server runs, whoever asks.</param>
/// <param name="what">What the jobs are, in the plural, for the message of a refusal.</param>
public sealed class ConcurrencyGate(int perUser, int total, string what)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<UserId, int> _perUser = [];
    private int _total;

    /// <summary>How many jobs are running.</summary>
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

    /// <summary>Takes a place for a job of <paramref name="user"/>, or says why there is none.</summary>
    /// <exception cref="GateBusyException">The user or the server is already running as many as are allowed.</exception>
    public IDisposable Enter(UserId user)
    {
        lock (_lock)
        {
            _perUser.TryGetValue(user, out int mine);
            if (mine >= perUser)
            {
                throw new GateBusyException($"You already have {mine} {what} running; the most is {perUser}. Try again when one has finished.");
            }
            if (_total >= total)
            {
                throw new GateBusyException($"The server is running as many {what} as it can; try again shortly.");
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

    private sealed class Lease(ConcurrencyGate gate, UserId user) : IDisposable
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
