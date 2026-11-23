using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class BlockSizeTests
{
    private static RenderedAudio Render(int blockFrames) =>
        OfflineRenderer.Render(
            BusyProject.Create(),
            BusyProject.Media(),
            new OfflineOptions { SampleRate = BusyProject.Rate, BlockFrames = blockFrames });

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(480)]
    [InlineData(4096)]
    [InlineData(1_000_000)]
    public void TheOutputIsIdenticalWhateverBlockSizeTheMixerWorksIn(int blockFrames)
    {
        RenderedAudio reference = Render(512);
        RenderedAudio other = Render(blockFrames);

        Assert.Equal(reference.Left, other.Left);
        Assert.Equal(reference.Right, other.Right);
    }

    [Fact]
    public void PullingBlocksOfUnevenLengthGivesTheSameAudioAsOneRender()
    {
        RenderedAudio reference = Render(512);
        RenderPlan plan = RenderPlan.Create(
            BusyProject.Create(),
            BusyProject.Media(),
            new PlanOptions { SampleRate = BusyProject.Rate });
        MixerGraph graph = new(plan, 128);
        float[] left = new float[reference.Frames];
        float[] right = new float[reference.Frames];
        int[] sizes = [1, 100, 333, 4000, 17, 700];

        for (int done = 0, turn = 0; done < left.Length; turn++)
        {
            int count = Math.Min(sizes[turn % sizes.Length], left.Length - done);
            graph.Render(left.AsSpan(done, count), right.AsSpan(done, count));
            done += count;
        }

        Assert.Equal(reference.Left, left);
        Assert.Equal(reference.Right, right);
        Assert.Equal(reference.Frames, graph.Position);
    }

    [Fact]
    public void TwoRendersOfTheSameProjectAreIdentical()
    {
        Assert.Equal(Render(512).Left, Render(512).Left);
    }

    [Fact]
    public void SeekingBackAndRenderingAgainRepeatsTheAudio()
    {
        RenderPlan plan = RenderPlan.Create(
            BusyProject.Create(),
            BusyProject.Media(),
            new PlanOptions { SampleRate = BusyProject.Rate });
        MixerGraph graph = new(plan);
        float[] first = new float[3000];
        float[] again = new float[3000];
        float[] unused = new float[3000];

        graph.Render(first, unused);
        graph.Seek(0);
        graph.Render(again, unused);

        Assert.Equal(first, again);
    }

    private static ProjectDocument WithAnEffectOnTheFirstTrack(AudioLab.Core.Rules.EffectKind kind)
    {
        ProjectDocument busy = BusyProject.Create();
        Track first = busy.Tracks[0] with { Effects = [AudioLab.Core.Rules.EffectRules.Create(kind, EffectId.From("e"))] };

        return busy with { Tracks = new ValueList<Track>([first, .. busy.Tracks.Skip(1)]) };
    }

    private static MixerGraph GraphWithAnInsertThatRemembers(int blockFrames) =>
        new(
            RenderPlan.Create(
                WithAnEffectOnTheFirstTrack(AudioLab.Core.Rules.EffectKind.Saturation),
                BusyProject.ResampledMedia(),
                new PlanOptions { SampleRate = BusyProject.Rate, InsertBuilder = (effect, context) => new OnePoleInsert() }),
            blockFrames);

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(777)]
    public void ResampledMediaAndAnInsertThatRemembersGiveTheSameAudioInAnyBlockSize(int blockFrames)
    {
        float[][] reference = [new float[4 * BusyProject.Rate], new float[4 * BusyProject.Rate]];
        float[][] other = [new float[4 * BusyProject.Rate], new float[4 * BusyProject.Rate]];

        GraphWithAnInsertThatRemembers(512).Render(reference[0], reference[1]);
        GraphWithAnInsertThatRemembers(blockFrames).Render(other[0], other[1]);

        Assert.Equal(reference[0], other[0]);
        Assert.Equal(reference[1], other[1]);
        Assert.True(Signals.Peak(reference[0]) > 0.05);
    }

    [Fact]
    public void ResampledMediaAndAnInsertThatRemembersAllocateNothingWhileRendering()
    {
        MixerGraph graph = GraphWithAnInsertThatRemembers(512);
        float[] left = new float[480];
        float[] right = new float[480];

        Assert.True(AllocationProbe.SettlesAtZero(() => graph.Render(left, right)));
    }
}
