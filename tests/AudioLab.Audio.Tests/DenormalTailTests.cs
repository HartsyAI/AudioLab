using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

/// <summary>A tail that decays for long must not hand denormal numbers, which are slow to compute with, to the next insert (D60).</summary>
public sealed class DenormalTailTests
{
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

    [Fact]
    public void ADecayingTailIsMadeSilenceBeforeItReachesTheNextInsert()
    {
        Probe probe = new();
        ProjectDocument document = TestProject.Create(
            TestProject.Track("a", TrackKind.Audio, TestProject.Clip("c", "m", 0, 0.5)) with
            {
                Effects = [EffectRulesFor("e1"), EffectRulesFor("e2")],
            });
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

    private static Effect EffectRulesFor(string id) => EffectRules.Create(EffectKind.Eq, EffectId.From(id));
}
