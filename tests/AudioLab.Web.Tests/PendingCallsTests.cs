using AudioLab.Web.Interop;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the calls waiting on the engine's worker.</summary>
public sealed class PendingCallsTests
{
    /// <summary>A call ends with the answer the worker gives, and an answer nobody waits for is ignored.</summary>
    [Fact]
    public async Task ACallEndsWithTheAnswerTheWorkerGivesAndAnUnknownAnswerIsIgnored()
    {
        PendingCalls calls = new();
        (int ok, Task done) = calls.Begin();
        (int refused, Task rejected) = calls.Begin();

        Assert.True(calls.Complete(ok, null));
        Assert.True(calls.Complete(refused, "No such call."));

        await done;
        AudioHostException error = await Assert.ThrowsAsync<AudioHostException>(() => rejected);
        Assert.Equal("No such call.", error.Message);
        Assert.False(calls.Complete(99, null));
        Assert.Equal(0, calls.Count);
    }

    /// <summary>When the worker goes, every call that waits fails with the reason, and none waits for ever.</summary>
    [Fact]
    public async Task WhenTheWorkerGoesEveryCallThatWaitsFailsWithTheReason()
    {
        PendingCalls calls = new();
        (_, Task first) = calls.Begin();
        (_, Task second) = calls.Begin();

        calls.FailAll("The worker crashed.");

        Assert.Equal("The worker crashed.", (await Assert.ThrowsAsync<AudioHostException>(() => first)).Message);
        Assert.Equal("The worker crashed.", (await Assert.ThrowsAsync<AudioHostException>(() => second)).Message);
        Assert.Equal(0, calls.Count);
    }
}
