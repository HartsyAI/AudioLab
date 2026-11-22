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
        for (int warmUp = 0; warmUp < 4; warmUp++)
        {
            graph.Render(left, right);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int block = 0; block < 40; block++)
        {
            graph.Render(left, right);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
