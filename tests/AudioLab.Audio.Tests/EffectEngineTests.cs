using AudioLab.Audio.Tests.Support;

namespace AudioLab.Audio.Tests;

public sealed class EffectEngineTests
{
    private static RenderedAudio Render(int blockFrames) =>
        OfflineRenderer.Render(
            EffectsProject.Create(),
            BusyProject.Media(),
            new OfflineOptions { SampleRate = BusyProject.Rate, BlockFrames = blockFrames });

    private static MixerGraph Graph(int blockFrames = MixerGraph.DefaultBlockFrames) =>
        new(
            RenderPlan.Create(EffectsProject.Create(), BusyProject.Media(), new PlanOptions { SampleRate = BusyProject.Rate }),
            blockFrames);

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(480)]
    [InlineData(777)]
    [InlineData(1_000_000)]
    public void EveryEffectGivesTheSameAudioWhateverBlockSizeTheMixerWorksIn(int blockFrames)
    {
        RenderedAudio reference = Render(512);
        RenderedAudio other = Render(blockFrames);

        Assert.Equal(reference.Left, other.Left);
        Assert.Equal(reference.Right, other.Right);
    }

    [Fact]
    public void TheRenderRunsOnPastTheContentUntilTheEffectsHaveRungOut()
    {
        RenderedAudio audio = Render(512);

        Assert.True(audio.Frames > 3.0 * BusyProject.Rate);
        Assert.Equal(0, Signals.Peak(audio.Left.AsSpan(audio.Frames - 8)), 4);
    }

    [Fact]
    public void SeekingBackClearsTheEffectsSoTheAudioRepeats()
    {
        MixerGraph graph = Graph();
        float[] first = new float[6000];
        float[] again = new float[6000];
        float[] unused = new float[6000];

        graph.Render(first, unused);
        graph.Seek(0);
        graph.Render(again, unused);

        Assert.Equal(first, again);
    }

    [Fact]
    public void RenderingABlockWithEveryEffectAllocatesNothing()
    {
        MixerGraph graph = Graph();
        float[] left = new float[480];
        float[] right = new float[480];
        Assert.True(AllocationProbe.SettlesAtZero(() => graph.Render(left, right)));
    }
}
