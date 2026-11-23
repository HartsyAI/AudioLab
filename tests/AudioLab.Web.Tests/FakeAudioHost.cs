using AudioLab.Core.Playback;
using AudioLab.Web.Interop;

namespace AudioLab.Web.Tests;

/// <summary>An engine host that records what the page asks of it and says what a test makes it say.</summary>
internal sealed class FakeAudioHost : IAudioHost
{
    public event Action<HostStatus>? StatusReceived;

    public event Action<WorkletStats>? WorkletReported;

    public event Action<string>? Failed;

    public event Action<string>? Rejected;

    public List<string> Calls { get; } = [];

    public List<(string Json, long Revision)> Documents { get; } = [];

    public Exception? StartFailure { get; set; }

    public bool Running { get; set; }

    public bool Available { get; set; } = true;

    public int SampleRate => 48000;

    public double BufferMilliseconds => 93;

    public Task StartAsync(string baseAddress, int budgetMilliseconds, CancellationToken cancellationToken)
    {
        Calls.Add($"start {baseAddress} {budgetMilliseconds}");
        if (StartFailure is not null)
        {
            throw StartFailure;
        }
        Running = true;
        return Task.CompletedTask;
    }

    public Task SetDocumentAsync(string json, long revision)
    {
        Documents.Add((json, revision));
        return Task.CompletedTask;
    }

    public void Play() => Record("play");

    public void Pause() => Record("pause");

    public void Stop() => Record("stop");

    public void Seek(double seconds) => Record($"seek {seconds:0.###}");

    public void SetLoop(bool on, double startSeconds, double endSeconds) => Record($"loop {on} {startSeconds:0.###} {endSeconds:0.###}");

    public void Dispose()
    {
        Calls.Add("dispose");
        Running = false;
    }

    // The real host does nothing for a transport call while it is not running, and so does this one.
    private void Record(string call)
    {
        if (Running)
        {
            Calls.Add(call);
        }
    }

    public void Report(HostStatus status) => StatusReceived?.Invoke(status);

    public void Report(WorkletStats stats) => WorkletReported?.Invoke(stats);

    public void Fail(string reason) => Failed?.Invoke(reason);

    public void Reject(string reason) => Rejected?.Invoke(reason);
}
