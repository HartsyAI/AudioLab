using System.Reflection;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

/// <summary>Holds the tests that a long decaying tail ends in no denormal numbers, between inserts or inside one (D60).</summary>
public sealed class DenormalTailTests
{
    private const int Rate = 48000;

    private sealed class SlowDecay : IInsert
    {
        private float _state;

        public double TailSeconds => 0;

        public void Reset() => _state = 0;

        public void Process(Span<float> left, Span<float> right, long startFrame)
        {
            for (int i = 0; i < left.Length; i++)
            {
                _state = (0.999f * _state) + (0.001f * left[i]);
                left[i] = _state;
                right[i] = _state;
            }
        }
    }

    private sealed class Probe : IInsert
    {
        public int Subnormal { get; private set; }

        public double TailSeconds => 0;

        public void Reset()
        {
        }

        public void Process(Span<float> left, Span<float> right, long startFrame)
        {
            foreach (float sample in left)
            {
                Subnormal += float.IsSubnormal(sample) ? 1 : 0;
            }
            foreach (float sample in right)
            {
                Subnormal += float.IsSubnormal(sample) ? 1 : 0;
            }
        }
    }

    private static Effect Eq(string id) => EffectRules.Create(EffectKind.Eq, EffectId.From(id));

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    // A state is either nothing or a number that arithmetic is not slow on.
    private static bool Settled(double value) => value == 0 || double.IsNormal(value);

    private static void Silence(IInsert insert, int seconds)
    {
        float[] left = new float[Rate];
        float[] right = new float[Rate];
        for (int second = 0; second < seconds; second++)
        {
            left.AsSpan().Clear();
            right.AsSpan().Clear();
            insert.Process(left, right, (long)second * Rate);
        }
    }

    private static void Burst(IInsert insert)
    {
        float[] left = Enumerable.Range(0, Rate / 2).Select(i => 0.9f * MathF.Sin(i * 0.05f)).ToArray();
        float[] right = [.. left];
        insert.Process(left, right, 0);
    }

    [Fact]
    public void ADecayingTailIsMadeSilenceBeforeItReachesTheNextInsert()
    {
        Probe probe = new();
        ProjectDocument document = TestProject.Create(
            TestProject.Track("a", TrackKind.Audio, TestProject.Clip("c", "m", 0, 0.5)) with { Effects = [Eq("e1"), Eq("e2")] });
        int built = 0;
        RenderPlan plan = RenderPlan.Create(
            document,
            new MemoryMedia().Add("m", Signals.Constant(0.5f, 1, Rendering.Rate)),
            new PlanOptions
            {
                SampleRate = Rendering.Rate,
                InsertBuilder = (effect, context) => built++ == 0 ? new SlowDecay() : probe,
            });
        MixerGraph graph = new(plan);
        float[] left = new float[Rendering.Rate];
        float[] right = new float[Rendering.Rate];

        for (int second = 0; second < 40; second++)
        {
            graph.Render(left, right);
        }

        Assert.Equal(0, probe.Subnormal);
    }

    [Fact]
    public void AFilterThatHasRungDownHoldsNothingButZeroOrAnOrdinaryNumber()
    {
        Biquad filter = new(BiquadCoefficients.HighPass(20, 0, Rate));
        float[] impulse = new float[Rate];
        impulse[0] = 1;

        filter.Process(impulse);
        for (int second = 0; second < 60; second++)
        {
            filter.Process(new float[Rate]);
        }

        Assert.True(Settled(Field<double>(filter, "_s1")));
        Assert.True(Settled(Field<double>(filter, "_s2")));
    }

    [Fact]
    public void ACompressorThatHasReleasedHoldsNothingButZeroOrAnOrdinaryNumber()
    {
        IInsert compressor = InsertFactoryFor(EffectRules.Create(EffectKind.Compressor, EffectId.From("c")));

        Burst(compressor);
        Silence(compressor, 600);

        Assert.True(Settled(Field<double>(compressor, "_reduction")));
    }

    [Fact]
    public void ADelayThatHasEchoedDownHoldsNothingButZeroOrAnOrdinaryNumber()
    {
        DelayEffect effect = (DelayEffect)EffectRules.Create(EffectKind.Delay, EffectId.From("d"));
        effect = effect with { Time = 0.05, Feedback = 0.9, Tone = 20000, Mix = 1 };
        IInsert delay = InsertFactoryFor(effect);

        Burst(delay);
        Silence(delay, 120);

        float[] lineLeft = Field<float[]>(delay, "_lineLeft");
        float[] lineRight = Field<float[]>(delay, "_lineRight");
        Assert.All([.. lineLeft, .. lineRight], sample => Assert.True(sample == 0 || float.IsNormal(sample)));
    }

    private static IInsert InsertFactoryFor(Effect effect) =>
        InsertFactory.Create(effect, new InsertContext(Rate, [new TempoStep(0, 120)]))!;
}
