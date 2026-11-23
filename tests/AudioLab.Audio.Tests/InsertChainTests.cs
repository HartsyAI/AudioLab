using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class InsertChainTests
{
    private static readonly MemoryMedia _media = new MemoryMedia().Add("m", Signals.Constant(0.4f, 1, Rendering.Rate));

    private static Effect Effect(string id) => EffectRules.Create(EffectKind.Saturation, EffectId.From(id));

    private static PlanOptions Options(Func<Effect, InsertContext, IInsert?> builder) =>
        new() { SampleRate = Rendering.Rate, InsertBuilder = builder };

    private static RenderedAudio Render(ProjectDocument document, StubInsert insert)
    {
        RenderPlan plan = RenderPlan.Create(document, _media, Options((effect, context) => insert));
        float[] left = new float[Rendering.Rate];
        float[] right = new float[Rendering.Rate];
        new MixerGraph(plan).Render(left, right);
        return new RenderedAudio(plan.Format, left, right);
    }

    [Fact]
    public void InsertsRunBeforeTheFaderAndBeforeAPreFaderSend()
    {
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with
        {
            Volume = 0.5,
            Effects = [Effect("e")],
            Sends = [new Send(TrackId.From("b"), 1, true, false)],
        };
        ProjectDocument document = TestProject.Create(track, TestProject.Track("b", TrackKind.Bus));

        RenderedAudio audio = Render(document, new StubInsert(0.5, 0));

        // 0.4 halved by the insert is 0.2; the pre send takes it whole and the fader halves the direct output.
        Assert.Equal(0.1f + 0.2f, Rendering.At(audio, 0.5), 5);
    }

    [Fact]
    public void AnInsertSeesTheTimelineFrameOfEachBlockAndIsResetBySeek()
    {
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with { Effects = [Effect("e")] };
        StubInsert insert = new(1, 0);
        MixerGraph graph = new(RenderPlan.Create(TestProject.Create(track), _media, Options((effect, context) => insert)), 100);
        float[] left = new float[250];
        float[] right = new float[250];

        graph.Seek(1000);
        graph.Render(left, right);

        Assert.Equal([1000L, 1100L, 1200L], insert.Starts);
        Assert.Equal(1, insert.Resets);
    }

    [Fact]
    public void TheTailIsTheTracksChainPlusTheBusItFeeds()
    {
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 1)) with
        {
            Effects = [Effect("e1")],
            Sends = [new Send(TrackId.From("b"), 1, false, false)],
        };
        Track bus = TestProject.Track("b", TrackKind.Bus) with { Effects = [Effect("e2")] };
        Dictionary<string, double> tails = new() { ["e1"] = 1, ["e2"] = 0.5 };

        RenderPlan plan = RenderPlan.Create(
            TestProject.Create(track, bus),
            _media,
            Options((effect, context) => new StubInsert(1, tails[effect.Id.Value])));

        Assert.Equal(1.5 * Rendering.Rate, plan.TailFrames);
    }

    private sealed class StubInsert(double gain, double tail) : IInsert
    {
        public List<long> Starts { get; } = [];

        public int Resets { get; private set; }

        public double TailSeconds => tail;

        public void Reset() => Resets++;

        public void Process(Span<float> left, Span<float> right, long startFrame)
        {
            Starts.Add(startFrame);
            for (int i = 0; i < left.Length; i++)
            {
                left[i] *= (float)gain;
                right[i] *= (float)gain;
            }
        }
    }
}
