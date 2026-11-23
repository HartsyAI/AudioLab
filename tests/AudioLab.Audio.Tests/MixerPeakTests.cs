using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class MixerPeakTests
{
    private const int Rate = 8000;

    private static RenderPlan PlanOf(Func<Track, Track> shape)
    {
        MemoryMedia media = new MemoryMedia().Add("m", Signals.Constant(0.5f, 1, Rate));
        Track loud = TestProject.Track("loud", TrackKind.Audio, TestProject.Clip("a", "m", 0, 1));
        Track other = TestProject.Track("other", TrackKind.Audio, TestProject.Clip("b", "m", 0, 1));
        return RenderPlan.Create(TestProject.Create(shape(loud), other), media, new PlanOptions { SampleRate = Rate });
    }

    [Fact]
    public void EachTrackAndTheMasterReportThePeakOfWhatTheyPutOutAndAReadingStartsTheNextOne()
    {
        RenderPlan plan = PlanOf(track => track with { Volume = 0.5 });
        MixerGraph graph = new(plan);
        float[] left = new float[256];
        float[] right = new float[256];
        float[] peaks = new float[graph.PeakCount];

        graph.Render(left, right);
        graph.TakePeaks(peaks);

        Assert.Equal(["loud", "other"], plan.TrackIds.Select(id => id.Value));
        Assert.Equal(6, peaks.Length);
        Assert.Equal(0.25f, peaks[0], 3);
        Assert.Equal(0.25f, peaks[1], 3);
        Assert.Equal(0.5f, peaks[2], 3);
        Assert.Equal(0.75f, peaks[4], 3);
        Assert.Equal(0.75f, peaks[5], 3);

        graph.TakePeaks(peaks);
        Assert.All(peaks, peak => Assert.Equal(0, peak));
    }

    [Fact]
    public void ATrackThatSoloSilencesIsLeftOutOfTheReadingAndTheMasterReadsWhatIsHeard()
    {
        RenderPlan plan = PlanOf(track => track with { Soloed = true });
        MixerGraph graph = new(plan);
        float[] peaks = new float[graph.PeakCount];

        graph.Render(new float[128], new float[128]);
        graph.TakePeaks(peaks);

        Assert.Equal(["loud"], plan.TrackIds.Select(id => id.Value));
        Assert.Equal(4, peaks.Length);
        Assert.Equal(0.5f, peaks[0], 3);
        Assert.Equal(0.5f, peaks[2], 3);
    }

    [Fact]
    public void ReadingPeaksAllocatesNothing()
    {
        MixerGraph graph = new(PlanOf(track => track));
        float[] left = new float[128];
        float[] right = new float[128];
        float[] peaks = new float[graph.PeakCount];

        bool silent = AllocationProbe.SettlesAtZero(() =>
        {
            graph.Render(left, right);
            graph.TakePeaks(peaks);
        });

        Assert.True(silent);
    }
}
