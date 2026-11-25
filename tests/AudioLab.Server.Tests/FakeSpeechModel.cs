using AudioLab.Core.Transcription;

namespace AudioLab.Server.Tests;

/// <summary>A speech model that counts its calls. Each window gets one segment unless a test gives its own answer or failure.</summary>
public sealed class FakeSpeechModel : ISpeechModel
{
    private TaskCompletionSource _entered = NewSignal();
    private TaskCompletionSource _release = NewSignal();
    private int? _blockAt;

    public string ModelId { get; init; } = "fake-model";

    /// <summary>The window starts the model was asked about, in call order.</summary>
    public List<double> Starts { get; } = [];

    /// <summary>The answer for a call, by call index; null for the default answer.</summary>
    public Func<int, SpeechWindow, IReadOnlyList<SpeechSegment>>? Answer { get; set; }

    /// <summary>The failure of a call, by call index; null for none.</summary>
    public Func<int, Exception?>? Fail { get; set; }

    /// <summary>How many calls the model has answered or started.</summary>
    public int Calls => Starts.Count;

    /// <summary>Completes when the blocked call has started.</summary>
    public Task Entered => _entered.Task;

    /// <summary>Holds the call with this index until <see cref="Release"/>.</summary>
    public void BlockAt(int call)
    {
        _blockAt = call;
        _entered = NewSignal();
        _release = NewSignal();
    }

    /// <summary>Lets the held call finish.</summary>
    public void Release() => _release.TrySetResult();

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        int call = Starts.Count;
        Starts.Add(window.StartSeconds);
        if (_blockAt == call)
        {
            _blockAt = null;
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }
        Exception? failure = Fail?.Invoke(call);
        if (failure is not null)
        {
            throw failure;
        }
        if (Answer is not null)
        {
            return Answer(call, window);
        }
        string text = "window " + call.ToString(System.Globalization.CultureInfo.InvariantCulture);
        List<SpeechSegment> one = [new SpeechSegment(1, 2, text, SpeechKind.Speech)];
        return one;
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
