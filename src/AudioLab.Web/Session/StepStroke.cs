using AudioLab.Web.Services;

namespace AudioLab.Web.Session;

/// <summary>One paint drag over a lane's steps. Every step it touches takes the same absolute state, the commands go out one at a time with the
/// drag's gesture, and a refusal stops the drag and takes back the steps it had painted but not written.</summary>
public sealed class StepStroke
{
    private readonly Func<IReadOnlyList<int>, bool, Task<EditResult>> _send;

    private readonly Dictionary<int, bool> _painted = [];

    private readonly List<int> _pending = [];

    private Task _running = Task.CompletedTask;

    /// <summary>Starts a drag that makes the steps it touches <paramref name="on"/>, sending each batch through <paramref name="send"/>.</summary>
    public StepStroke(bool on, string gesture, Func<IReadOnlyList<int>, bool, Task<EditResult>> send)
    {
        ArgumentNullException.ThrowIfNull(send);
        On = on;
        Gesture = gesture;
        _send = send;
    }

    /// <summary>The state every step of the drag is set to.</summary>
    public bool On { get; }

    /// <summary>The gesture the drag's commands are sent with, closed when the drag ends.</summary>
    public string Gesture { get; }

    /// <summary>Whether the drag has ended, so that no step is painted any more.</summary>
    public bool Ended { get; private set; }

    /// <summary>The steps the drag shows as painted, with the state each shows; a step the server has not yet confirmed is still here.</summary>
    public IReadOnlyDictionary<int, bool> Painted => _painted;

    /// <summary>The words the server refused a batch with, or null when every batch so far was applied.</summary>
    public string? Refusal { get; private set; }

    /// <summary>Raised when a refusal takes back painted steps, so the view can show them as they are.</summary>
    public event Action? Changed;

    /// <summary>Completes when every batch sent so far has been answered.</summary>
    public Task Done => _running;

    /// <summary>Paints a step toward the drag's state. Returns true when the step's shown state changes.</summary>
    /// <param name="step">The step under the pointer.</param>
    /// <param name="shown">The state the step shows now, with the paint of this drag.</param>
    public bool Paint(int step, bool shown)
    {
        if (Ended || Refusal is not null || shown == On || _painted.ContainsKey(step))
        {
            return false;
        }

        _painted[step] = On;
        _pending.Add(step);
        if (_running.IsCompleted)
        {
            _running = SendPendingAsync();
        }

        return true;
    }

    /// <summary>Cancels the drag, as Escape does: the steps painted but not yet sent are withdrawn, and no step is painted after this. A batch
    /// already on its way stays, as an applied batch does; the returned task of <see cref="EndAsync"/> completes once it has been answered.</summary>
    public void Cancel()
    {
        if (Ended)
        {
            return;
        }

        Ended = true;
        foreach (int step in _pending)
        {
            _painted.Remove(step);
        }

        _pending.Clear();
        Changed?.Invoke();
    }

    /// <summary>Ends the drag: no step is painted after this. The returned task completes once what was painted has been sent.</summary>
    public Task EndAsync()
    {
        Ended = true;
        return _running;
    }

    // One batch at a time: the steps painted while a batch is on its way go out in the next one, as one command.
    private async Task SendPendingAsync()
    {
        while (_pending.Count > 0 && Refusal is null)
        {
            int[] batch = [.. _pending];
            _pending.Clear();
            EditResult result = await _send(batch, On);
            if (!result.Succeeded)
            {
                Refusal = result.Refusal ?? "The steps were not changed.";
                foreach (int step in batch)
                {
                    _painted.Remove(step);
                }

                foreach (int step in _pending)
                {
                    _painted.Remove(step);
                }

                _pending.Clear();
                Changed?.Invoke();
            }
        }
    }
}
