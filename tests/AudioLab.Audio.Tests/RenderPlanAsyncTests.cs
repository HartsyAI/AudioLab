using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

/// <summary>Holds the tests of the plan that is built in slices, with a pause between tracks for a caller that has work of its own (D60).</summary>
public sealed class RenderPlanAsyncTests
{
    // A clock that moves a second at each reading, so every track is a slice long.
    private static SteppingTime Slow() => new(TimeSpan.FromSeconds(1));

    private static float[] Mix(RenderPlan plan)
    {
        MixerGraph graph = new(plan);
        float[] left = new float[BusyProject.Rate];
        float[] right = new float[BusyProject.Rate];
        graph.Render(left, right);
        return [.. left, .. right];
    }

    [Fact]
    public async Task APlanBuiltInSlicesSoundsTheSameAsOneBuiltAtOnceAndWaitsBetweenTracks()
    {
        ProjectDocument document = BusyProject.Create();
        PlanOptions options = new() { SampleRate = BusyProject.Rate };
        int pauses = 0;

        RenderPlan sliced = await RenderPlan.CreateAsync(
            document,
            BusyProject.Media(),
            options,
            _ =>
            {
                pauses++;
                return Task.CompletedTask;
            },
            Slow());
        RenderPlan whole = RenderPlan.Create(document, BusyProject.Media(), options);

        Assert.True(pauses >= 1);
        Assert.Equal(Mix(whole), Mix(sliced));
    }

    [Fact]
    public async Task ABuildThatIsFasterThanASliceDoesNotWaitAtAll()
    {
        int pauses = 0;

        await RenderPlan.CreateAsync(
            BusyProject.Create(),
            BusyProject.Media(),
            new PlanOptions { SampleRate = BusyProject.Rate },
            _ =>
            {
                pauses++;
                return Task.CompletedTask;
            },
            new SteppingTime(TimeSpan.Zero));

        Assert.Equal(0, pauses);
    }

    [Fact]
    public async Task ABuildCancelledInItsOnlyPauseEndsWithoutAPlan()
    {
        using CancellationTokenSource cancellation = new();
        ProjectDocument document = TestProject.Create(TestProject.Track("a", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)));
        PlanOptions options = new() { SampleRate = BusyProject.Rate, CancellationToken = cancellation.Token };
        int pauses = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RenderPlan.CreateAsync(
            document,
            new MemoryMedia().Add("m", Signals.Constant(0.5f, 1, BusyProject.Rate)),
            options,
            _ =>
            {
                pauses++;
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            Slow()));

        Assert.Equal(1, pauses);
    }

    [Fact]
    public async Task ABuildCancelledBetweenTracksStopsBeforeTheNextOne()
    {
        using CancellationTokenSource cancellation = new();
        PlanOptions options = new() { SampleRate = BusyProject.Rate, CancellationToken = cancellation.Token };
        int pauses = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RenderPlan.CreateAsync(
            BusyProject.Create(),
            BusyProject.Media(),
            options,
            _ =>
            {
                pauses++;
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            Slow()));

        Assert.Equal(1, pauses);
    }
}
