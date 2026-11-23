using AudioLab.Audio.Tests.Support;

namespace AudioLab.Audio.Tests;

public sealed class AllocationTests
{
    [Fact]
    public void RenderingABlockAllocatesNothing()
    {
        RenderPlan plan = RenderPlan.Create(
            BusyProject.Create(),
            BusyProject.Media(),
            new PlanOptions { SampleRate = BusyProject.Rate });
        MixerGraph graph = new(plan);
        float[] left = new float[480];
        float[] right = new float[480];
        Assert.True(AllocationProbe.SettlesAtZero(() => graph.Render(left, right)));
    }
}
