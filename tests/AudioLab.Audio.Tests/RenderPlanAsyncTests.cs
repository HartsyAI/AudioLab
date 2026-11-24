using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class RenderPlanAsyncTests
{
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

        RenderPlan sliced = await RenderPlan.CreateAsync(document, BusyProject.Media(), options, _ =>
        {
            pauses++;
            return Task.CompletedTask;
        });
        RenderPlan whole = RenderPlan.Create(document, BusyProject.Media(), options);

        Assert.True(pauses >= 1);
        Assert.Equal(Mix(whole), Mix(sliced));
    }

    [Fact]
    public async Task ACancelledSliceStopsTheBuild()
    {
        using CancellationTokenSource cancellation = new();
        PlanOptions options = new() { SampleRate = BusyProject.Rate, CancellationToken = cancellation.Token };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RenderPlan.CreateAsync(
            BusyProject.Create(), BusyProject.Media(), options, _ =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            }));
    }
}
