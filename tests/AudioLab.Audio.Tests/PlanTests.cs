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
        public ITrackSource? Create(ProjectDocument document, Track track, AudioFormat format, IPlanMedia media) => source;
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

    [Fact]
    public void AMissingFileThatASourceAsksForFailsThePlanLikeAMissingClip()
    {
        ProjectDocument document = TestProject.Create(TestProject.Track("i", TrackKind.Instrument));

        MissingMediaException error = Assert.Throws<MissingMediaException>(
            () => RenderPlan.Create(document, new MemoryMedia(), Options(new AskingFactory("sample"))));

        Assert.Equal(["sample"], error.Missing.Select(id => id.Value));
    }

    [Fact]
    public void AFileASourceAsksForComesAtThePlansRateAndWithoutNaN()
    {
        PcmAudio odd = new(11025, [Enumerable.Repeat(0.5f, 11025).Append(float.NaN).ToArray()]);
        ProjectDocument document = TestProject.Create(TestProject.Track("i", TrackKind.Instrument));
        AskingFactory factory = new("sample");

        RenderPlan.Create(document, new MemoryMedia().Add("sample", odd), Options(factory));

        Assert.Equal(Rendering.Rate, factory.Got!.SampleRate);
        Assert.All(factory.Got.Channel(0).ToArray(), sample => Assert.True(float.IsFinite(sample)));
    }

    [Fact]
    public void AMutedOrSoloSilencedTrackDoesNotLengthenTheRender()
    {
        MemoryMedia media = new MemoryMedia().Add("m", Signals.Constant(1, 3, Rendering.Rate));
        ProjectDocument document = TestProject.Create(
            TestProject.Track("heard", TrackKind.Audio, TestProject.Clip("c1", "m", 0, 1)) with { Soloed = true },
            TestProject.Track("muted", TrackKind.Audio, TestProject.Clip("c2", "m", 0, 3)) with { Muted = true },
            TestProject.Track("quiet", TrackKind.Audio, TestProject.Clip("c3", "m", 0, 2)));

        RenderPlan plan = RenderPlan.Create(document, media, Options());

        Assert.Equal(Rendering.Rate, plan.ContentFrames);
    }

    [Fact]
    public void TwoClipsWithTheSameIdOnATrackAreRefusedClearly()
    {
        MemoryMedia media = new MemoryMedia().Add("m", Signals.Constant(1, 3, Rendering.Rate));
        ProjectDocument document = TestProject.Create(
            TestProject.Track("t", TrackKind.Audio, TestProject.Clip("same", "m", 0, 1), TestProject.Clip("same", "m", 2, 1)));

        ArgumentException error = Assert.Throws<ArgumentException>(() => RenderPlan.Create(document, media, Options()));

        Assert.Contains("same", error.Message);
    }

    [Fact]
    public void ARenderThatEndsBeforeItStartsIsRefused()
    {
        ProjectDocument document = TestProject.Create();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => OfflineRenderer.Render(document, new MemoryMedia(), new OfflineOptions { Start = 2, End = 1 }));
    }

    [Fact]
    public void MediaWithSamplesThatAreNotNumbersNeverReachesTheMasterOrTheLimiter()
    {
        float[] samples = [.. Enumerable.Repeat(0.5f, 4000)];
        samples[100] = float.NaN;
        samples[200] = float.PositiveInfinity;
        MemoryMedia media = new MemoryMedia().Add("m", new PcmAudio(Rendering.Rate, [samples, samples]));
        ProjectDocument document = TestProject.Create(TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 0.5))) with
        {
            MasterLimiter = true,
        };

        RenderedAudio audio = Rendering.Render(document, media);

        Assert.All(audio.Left, sample => Assert.True(float.IsFinite(sample)));
        Assert.Equal(0.5f, audio.Left[3000], 6);
    }

    private sealed class AskingFactory(string media) : ITrackSourceFactory
    {
        public PcmAudio? Got { get; private set; }

        public ITrackSource? Create(ProjectDocument document, Track track, AudioFormat format, IPlanMedia planMedia)
        {
            Got = planMedia.Get(MediaId.From(media));
            return null;
        }
    }
}
