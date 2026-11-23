namespace AudioLab.Web.Components;

/// <summary>Raises toasts from any component or service; <see cref="ToastHost"/> shows them.</summary>
public sealed class ToastService(TimeProvider clock) : IDisposable
{
    /// <summary>How long a toast that is not an error stays before it goes by itself.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(6);

    private readonly List<ToastItem> _items = [];

    private readonly Dictionary<string, ITimer> _timers = [];

    private long _next;

    /// <summary>Raised when a toast comes or goes.</summary>
    public event Action? Changed;

    /// <summary>The toasts that are showing, oldest first.</summary>
    public IReadOnlyList<ToastItem> Items => _items;

    /// <summary>Shows a toast that says what happened, with Undo when there is one. An error stays until dismissed.</summary>
    /// <remarks>A toast raised while a modal dialog is open cannot be pressed until it closes, so one raised there must not rely on Undo.</remarks>
    public string Show(ToastKind kind, string message, Func<Task>? undo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        string id = $"toast-{++_next}";
        _items.Add(new ToastItem(id, kind, message, undo));
        if (kind != ToastKind.Error)
        {
            _timers[id] = clock.CreateTimer(_ => Dismiss(id), null, Lifetime, Timeout.InfiniteTimeSpan);
        }
        Changed?.Invoke();
        return id;
    }

    /// <summary>Removes a toast; one that is already gone is left alone.</summary>
    public void Dismiss(string id)
    {
        if (_items.RemoveAll(item => item.Id == id) > 0)
        {
            Forget(id);
            Changed?.Invoke();
        }
    }

    /// <summary>Runs the toast's Undo and removes the toast.</summary>
    public async Task UndoAsync(string id)
    {
        ToastItem? toast = _items.Find(item => item.Id == id);
        Dismiss(id);
        if (toast?.Undo is not null)
        {
            await toast.Undo();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (ITimer timer in _timers.Values)
        {
            timer.Dispose();
        }
        _timers.Clear();
    }

    private void Forget(string id)
    {
        if (_timers.Remove(id, out ITimer? timer))
        {
            timer.Dispose();
        }
    }
}
