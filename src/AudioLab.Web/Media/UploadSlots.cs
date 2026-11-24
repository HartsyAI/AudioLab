namespace AudioLab.Web.Media;

/// <summary>The places for uploads that run at once, in the order they were asked for, whose number can change while jobs wait for one.</summary>
/// <param name="limit">How many uploads may run at once to begin with.</param>
internal sealed class UploadSlots(int limit)
{
    private readonly Lock _gate = new();

    private readonly Queue<TaskCompletionSource> _waiting = new();

    private int _limit = Math.Max(1, limit);

    private int _running;

    /// <summary>How many uploads may run at once now.</summary>
    public int Limit
    {
        get
        {
            lock (_gate)
            {
                return _limit;
            }
        }
    }

    /// <summary>Changes how many uploads may run at once; running ones finish, and waiting ones start if there is room.</summary>
    public void SetLimit(int limit)
    {
        lock (_gate)
        {
            _limit = Math.Max(1, limit);
        }
        Pump();
    }

    /// <summary>Waits for a place; the caller gives it back with <see cref="Release"/>. Cancelling stops the wait and takes no place.</summary>
    public async Task AcquireAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_running < _limit && _waiting.Count == 0)
            {
                _running++;
                return;
            }
            _waiting.Enqueue(waiter);
        }
        using (cancellationToken.Register(() => waiter.TrySetCanceled(cancellationToken)))
        {
            await waiter.Task;
        }
    }

    /// <summary>Gives back a place that <see cref="AcquireAsync"/> gave.</summary>
    public void Release()
    {
        lock (_gate)
        {
            _running--;
        }
        Pump();
    }

    private void Pump()
    {
        lock (_gate)
        {
            while (_running < _limit && _waiting.TryDequeue(out TaskCompletionSource? waiter))
            {
                // A wait that was cancelled while it queued takes no place.
                _running++;
                if (!waiter.TrySetResult())
                {
                    _running--;
                }
            }
        }
    }
}
