namespace AudioLab.Web.Components;

/// <summary>Sends the value a slider was set to, one send at a time, and holds the value it shows until the value that answers it is drawn.</summary>
/// <remarks>
/// A key that arrives while a send is on its way is folded into the next send, so a held key sends one value per answer. A refused send never
/// changes the value, so the held value goes after a moment when nothing has come back.
/// </remarks>
public sealed class ValueSender : IDisposable
{
    private static readonly TimeSpan _defaultSettle = TimeSpan.FromMilliseconds(400);

    private readonly TimeSpan _settleFor;

    private readonly Func<double, Task> _send;

    private readonly Action _changed;

    private double _wanted = double.NaN;

    private double _sent = double.NaN;

    private CancellationTokenSource? _settle;

    /// <summary>Starts a sender that passes each value to send, and calls changed when what is shown changes.</summary>
    public ValueSender(Func<double, Task> send, Action changed, TimeSpan? settle = null)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _settleFor = settle ?? _defaultSettle;
    }

    /// <summary>The value held while it is sent, from 0 to 1, or null when the drawn value is shown.</summary>
    public double? Held { get; private set; }

    /// <summary>Whether a send is on its way.</summary>
    public bool Sending { get; private set; }

    /// <summary>Holds <paramref name="target"/> and sends it, or folds it into the send that is on its way.</summary>
    public async Task SendAsync(double target)
    {
        Held = target;
        _wanted = target;
        _changed();
        if (Sending)
        {
            return;
        }
        Sending = true;
        try
        {
            while (_sent != _wanted)
            {
                _sent = _wanted;
                await _send(_sent);
            }
        }
        finally
        {
            Sending = false;
            _sent = double.NaN;
        }
        await LetGoAsync();
    }

    /// <summary>Drops the held value when the drawn value at <paramref name="position"/> is the one it holds and no send is on its way.</summary>
    public void Arrived(double position)
    {
        if (Held is { } held && !Sending && Math.Abs(position - held) < 1e-9)
        {
            Held = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _settle?.Cancel();
        _settle?.Dispose();
    }

    // A refused send never changes the value, so the held value must not stay for ever: it goes after a moment if the value did not come.
    private async Task LetGoAsync()
    {
        _settle?.Cancel();
        _settle?.Dispose();
        CancellationTokenSource settle = new();
        _settle = settle;
        try
        {
            await Task.Delay(_settleFor, settle.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        Held = null;
        _changed();
    }
}
