using AudioLab.Server.Realtime;

namespace AudioLab.Server.Tests;

public sealed class ConnectionOutboxTests
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AClientThatIsBehindIsSentTheLatestChangeOfEachProjectAndNotTheOnesBetween()
    {
        FakeProjectClient client = new();
        using ConnectionOutbox outbox = new(new FakeHubContext(client), "c", () => { }, TimeSpan.FromSeconds(30), NullLogger());
        client.Close();
        outbox.Post(Change("a", 1));
        await client.Started.Task.WaitAsync(_wait);
        for (int revision = 2; revision <= 10; revision++)
        {
            outbox.Post(Change("a", revision));
            outbox.Post(Change("b", revision));
        }
        outbox.Post(Change("a", 4));
        client.Open();

        await WaitForAsync(client, 3);
        Assert.Equal(["a:1", "a:10", "b:10"], client.Sent);
    }

    [Fact]
    public async Task ADeletionIsNeverReplacedAndSupersedesTheChangesBeforeAndAfterIt()
    {
        FakeProjectClient client = new();
        using ConnectionOutbox outbox = new(new FakeHubContext(client), "c", () => { }, TimeSpan.FromSeconds(30), NullLogger());
        client.Close();
        outbox.Post(Change("busy", 1));
        await client.Started.Task.WaitAsync(_wait);
        outbox.Post(Change("gone", 5));
        outbox.Post(Change("kept", 7));
        outbox.PostDeleted(new ProjectClosed("gone", "deleted"));
        outbox.Post(Change("gone", 6));
        outbox.PostDeleted(new ProjectClosed("gone", "deleted"));
        client.Open();

        await WaitForAsync(client, 3);
        Assert.Equal(["busy:1", "gone:deleted", "kept:7"], client.Sent);
    }

    [Fact]
    public async Task AClientThatTakesNothingForTheSendTimeoutIsAbortedAndPostingNeverWaits()
    {
        FakeProjectClient client = new();
        TaskCompletionSource aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ConnectionOutbox outbox = new(new FakeHubContext(client), "c", () => aborted.TrySetResult(), TimeSpan.FromMilliseconds(200), NullLogger());
        client.Close();
        outbox.Post(Change("a", 1));
        await client.Started.Task.WaitAsync(_wait);
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        for (int revision = 2; revision < 5000; revision++)
        {
            outbox.Post(Change("a", revision));
        }
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), "posting waited for the client");

        await aborted.Task.WaitAsync(_wait);
        outbox.Post(Change("a", 6000));
        outbox.PostDeleted(new ProjectClosed("a", "deleted"));
    }

    [Fact]
    public async Task AClientWhoseSendFailsForAnyReasonIsAbortedNotLeftConnectedAndDeaf()
    {
        FakeProjectClient client = new();
        client.FailWith(new InvalidOperationException("the connection is in a bad state"));
        TaskCompletionSource aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ConnectionOutbox outbox = new(new FakeHubContext(client), "c", () => aborted.TrySetResult(), TimeSpan.FromSeconds(30), NullLogger());
        outbox.Post(Change("a", 1));
        await aborted.Task.WaitAsync(_wait);
        outbox.Post(Change("a", 2));
        outbox.PostDeleted(new ProjectClosed("a", "deleted"));
    }

    [Fact]
    public async Task ASendThatFaultsWithACancellationWhileTheOutboxIsRunningStillAbortsTheClient()
    {
        FakeProjectClient client = new();
        client.FailWith(new TaskCanceledException("the connection is closing"));
        TaskCompletionSource aborted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ConnectionOutbox outbox = new(new FakeHubContext(client), "c", () => aborted.TrySetResult(), TimeSpan.FromSeconds(30), NullLogger());
        outbox.Post(Change("a", 1));
        await aborted.Task.WaitAsync(_wait);
    }

    [Fact]
    public async Task AnAbortThatThrowsDoesNotLeaveTheWriterFaultedAndPostingStaysHarmless()
    {
        FakeProjectClient client = new();
        client.FailWith(new InvalidOperationException("bad state"));
        TaskCompletionSource called = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ConnectionOutbox outbox = new(new FakeHubContext(client), "c", () =>
        {
            called.TrySetResult();
            throw new ObjectDisposedException("connection");
        }, TimeSpan.FromSeconds(30), NullLogger());
        outbox.Post(Change("a", 1));
        await called.Task.WaitAsync(_wait);
        await Task.Delay(100);
        outbox.Post(Change("a", 2));
        outbox.PostDeleted(new ProjectClosed("a", "deleted"));
    }

    [Fact]
    public async Task AnOutboxThatIsDisposedAbortsNothing()
    {
        FakeProjectClient client = new();
        int aborts = 0;
        ConnectionOutbox outbox = new(new FakeHubContext(client), "c", () => Interlocked.Increment(ref aborts), TimeSpan.FromSeconds(30), NullLogger());
        client.Close();
        outbox.Post(Change("a", 1));
        await client.Started.Task.WaitAsync(_wait);
        outbox.Dispose();
        await Task.Delay(200);
        Assert.Equal(0, aborts);
    }

    private static ProjectChange Change(string project, long revision) => new(project, revision, "{}", "test");

    private static Microsoft.Extensions.Logging.ILogger NullLogger() => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    private static async Task WaitForAsync(FakeProjectClient client, int count)
    {
        for (int i = 0; i < 200 && client.Sent.Count < count; i++)
        {
            await Task.Delay(25);
        }
        await Task.Delay(100);
    }
}
