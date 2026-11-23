using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class SourceNaNTests
{
    private const int Rate = 8000;

    private sealed class Bad : ITrackSource, ITrackSourceFactory
    {
        public bool IsMono => false;

        public double TailSeconds => 0;

        public void Reset()
        {
        }

        public void Render(long startFrame, Span<float> left, Span<float> right)
        {
            for (int i = 0; i < left.Length; i++)
            {
                long frame = startFrame + i;
                float value = frame == 300 ? float.NaN : frame == 301 ? float.PositiveInfinity : (float)(0.3 * Math.Sin(frame * 0.2));
                left[i] += value;
                right[i] += value;
            }
        }

        public ITrackSource? Create(ProjectDocument document, Track track, AudioFormat format, IPlanMedia media) => this;
    }

    [Theory]
    [InlineData(EffectKind.Eq)]
    [InlineData(EffectKind.Compressor)]
    [InlineData(EffectKind.Reverb)]
    [InlineData(EffectKind.Delay)]
    [InlineData(EffectKind.Saturation)]
    public void ASampleThatIsNotANumberFromASourceNeverPoisonsAnEffect(EffectKind kind)
    {
        Bad source = new();
        Track track = TestProject.Track("i", TrackKind.Instrument) with { Effects = [EffectRules.Create(kind, EffectId.From("e"))] };
        PlanOptions options = new() { SampleRate = Rate, Sources = source };
        MixerGraph graph = new(RenderPlan.Create(TestProject.Create(track), new MemoryMedia(), options));
        float[] left = new float[Rate];
        float[] right = new float[Rate];

        graph.Render(left, right);

        Assert.All(left, sample => Assert.True(float.IsFinite(sample)));
        Assert.All(right, sample => Assert.True(float.IsFinite(sample)));
        Assert.True(Signals.Peak(left.AsSpan(Rate / 2)) > 0.01);
    }
}
