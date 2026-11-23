using AudioLab.Core.Playback;
using AudioLab.Web.Interop;

namespace AudioLab.Web.Tests;

/// <summary>An engine host that records what the page asks of it and says what a test makes it say.</summary>
internal sealed class FakeAudioHost : IAudioHost
{
    public event Action<HostStatus>? StatusReceived;

    public event Action<WorkletStats>? WorkletReported;

    public event Action<string>? Failed;

    public List<string> Calls { get; } = [];

    public List<(string Json, long Revision)> Documents { get; } = [];

    public Exception? StartFailure { get; set; }

    public bool Running { get; private set; }

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

    public void Play() => Calls.Add("play");

    public void Pause() => Calls.Add("pause");

    public void Stop() => Calls.Add("stop");

    public void Seek(double seconds) => Calls.Add($"seek {seconds:0.###}");

    public void SetLoop(bool on, double startSeconds, double endSeconds) => Calls.Add($"loop {on} {startSeconds:0.###} {endSeconds:0.###}");

    public void Dispose() => Calls.Add("dispose");

    public void Report(HostStatus status) => StatusReceived?.Invoke(status);

    public void Report(WorkletStats stats) => WorkletReported?.Invoke(stats);

    public void Fail(string reason) => Failed?.Invoke(reason);
}
