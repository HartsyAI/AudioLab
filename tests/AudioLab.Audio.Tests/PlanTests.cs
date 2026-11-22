using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class PlanTests
{
    private static PlanOptions Options(ITrackSourceFactory? sources = null) =>
        new() { SampleRate = Rendering.Rate, Sources = sources };

    [Fact]
    public void MediaTheSourceCannotSupplyIsReportedTogetherBeforeAnythingRenders()
    {
        ProjectDocument document = TestProject.Create(
            TestProject.Track("a", TrackKind.Audio, TestProject.Clip("c1", "gone1", 0, 1), TestProject.Clip("c2", "gone2", 2, 1)));

        MissingMediaException error = Assert.Throws<MissingMediaException>(() => RenderPlan.Create(document, new MemoryMedia(), Options()));

        Assert.Equal(["gone1", "gone2"], error.Missing.Select(id => id.Value));
    }

    [Fact]
    public void AMutedTrackOrClipNeedsNoMedia()
    {
        ProjectDocument document = TestProject.Create(
            TestProject.Track("a", TrackKind.Audio, TestProject.Clip("c1", "gone1", 0, 1)) with { Muted = true },
            TestProject.Track("b", TrackKind.Audio, TestProject.Clip("c2", "gone2", 0, 1) with { Muted = true }));

        RenderPlan plan = RenderPlan.Create(document, new MemoryMedia(), Options());

        Assert.NotNull(plan);
    }

    [Fact]
    public void AnEnabledEffectIsRefusedUntilItsDspExists()
    {
        Track track = TestProject.Track("a") with { Effects = [EffectRules.Create(EffectKind.Reverb, EffectId.From("e1"))] };

        Assert.Throws<NotSupportedException>(() => RenderPlan.Create(TestProject.Create(track), new MemoryMedia(), Options()));
    }

    [Fact]
    public void ABypassedEffectIsLeftOut()
    {
        Effect off = EffectRules.Create(EffectKind.Reverb, EffectId.From("e1")) with { Enabled = false };
        Track track = TestProject.Track("a") with { Effects = [off] };

        RenderPlan plan = RenderPlan.Create(TestProject.Create(track), new MemoryMedia(), Options());

        Assert.Equal(0, plan.TailFrames);
    }

    [Fact]
    public void TheContentEndsWhereTheLastUnmutedClipEnds()
    {
        ProjectDocument document = TestProject.Create(
            TestProject.Track(
                "a",
                TrackKind.Audio,
                TestProject.Clip("c1", "m", 0, 1),
                TestProject.Clip("c2", "m", 2, 1.5),
                TestProject.Clip("c3", "m", 9, 1) with { Muted = true }));

        RenderPlan plan = RenderPlan.Create(document, new MemoryMedia().Add("m", Signals.Constant(1, 3, Rendering.Rate)), Options());

        Assert.Equal(3.5 * Rendering.Rate, plan.ContentFrames);
    }

    [Fact]
    public void AnInstrumentTrackIsSilentWithoutASourceAndPlaysThroughTheMixerWithOne()
    {
        ProjectDocument document = TestProject.Create(TestProject.Track("i", TrackKind.Instrument));
        StubSource source = new();
        float[] left = new float[512];
        float[] right = new float[512];

        new MixerGraph(RenderPlan.Create(document, new MemoryMedia(), Options())).Render(left, right);
        Assert.Equal(0, Signals.Peak(left));

        MixerGraph graph = new(RenderPlan.Create(document, new MemoryMedia(), Options(new StubFactory(source))));
        graph.Seek(1000);
        graph.Render(left, right);

        Assert.Equal(0.25f, left[0], 6);
        Assert.Equal(0f, right[0], 6);
        Assert.Equal(1000L, source.LastStart);
        Assert.Equal(1, source.Resets);
    }

    [Fact]
    public void ASourceTailLengthensTheRender()
    {
        ProjectDocument document = TestProject.Create(TestProject.Track("i", TrackKind.Instrument));

        RenderPlan plan = RenderPlan.Create(document, new MemoryMedia(), Options(new StubFactory(new StubSource())));

        Assert.Equal(Rendering.Rate / 2, plan.TailFrames);
    }

    private sealed class StubFactory(ITrackSource source) : ITrackSourceFactory
    {
        public ITrackSource? Create(ProjectDocument document, Track track, AudioFormat format) => source;
    }

    private sealed class StubSource : ITrackSource
    {
        public bool IsMono => false;

        public int Resets { get; private set; }

        public long LastStart { get; private set; }

        public double TailSeconds => 0.5;

        public void Reset() => Resets++;

        public void Render(long startFrame, Span<float> left, Span<float> right)
        {
            LastStart = startFrame;
            left.Fill(0.25f);
        }
    }
}
