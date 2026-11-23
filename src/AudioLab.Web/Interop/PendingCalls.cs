namespace AudioLab.Web.Interop;

/// <summary>The calls made to the engine's worker that it has not answered yet, so that none is left waiting when the worker goes.</summary>
/// <remarks>Blazor WebAssembly runs on one thread, so nothing here locks.</remarks>
public sealed class PendingCalls
{
    private readonly Dictionary<int, TaskCompletionSource> _calls = [];
    private int _last;

    /// <summary>How many calls wait for an answer.</summary>
    public int Count => _calls.Count;

    /// <summary>Makes a call to wait for; the task ends with <see cref="Complete"/> or <see cref="FailAll"/>.</summary>
    public (int Id, Task Done) Begin()
    {
        int id = ++_last;
        TaskCompletionSource source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _calls[id] = source;
        return (id, source.Task);
    }

    /// <summary>Ends a call with the answer: <paramref name="error"/> is why it was refused, or null; false when nobody waits for it.</summary>
    public bool Complete(int id, string? error)
    {
        if (!_calls.Remove(id, out TaskCompletionSource? source))
        {
            return false;
        }
        if (error is null)
        {
            source.TrySetResult();
        }
        else
        {
            source.TrySetException(new AudioHostException(error));
        }
        return true;
    }

    /// <summary>Ends every call that waits, with a failure that says why.</summary>
    public void FailAll(string reason)
    {
        foreach (TaskCompletionSource source in _calls.Values)
        {
            source.TrySetException(new AudioHostException(reason));
        }
        _calls.Clear();
    }
}
